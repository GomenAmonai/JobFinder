using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using JobRadar.Api.Hubs;
using JobRadar.Api.Ingestion;
using JobRadar.Api;
using JobRadar.Application.Applications;
using JobRadar.Application.Auth;
using JobRadar.Application.Employer;
using JobRadar.Application.Ingestion;
using JobRadar.Application.SavedFilters;
using JobRadar.Application.Vacancies;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure;
using JobRadar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

const string SpaCorsPolicy = "spa";
// Origins из конфигурации (Cors:AllowedOrigins), дефолт — dev-SPA. В проде задать HTTPS-origin.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // AllowCredentials нужен для SignalR

builder.Services.AddOpenApi();
// Необработанные исключения отдаём как ProblemDetails (без стек-трейса наружу).
builder.Services.AddProblemDetails();
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection(KafkaSettings.SectionName));
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Статусы откликов ездят по REST как строки, а не как числа.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHostedService<VacancyChangedConsumer>();

var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Missing 'Jwt' configuration.");
// Fail-fast в не-Development: ни дев-плейсхолдер, ни слишком короткий ключ (HS256 требует >=256 бит).
if (!builder.Environment.IsDevelopment() &&
    (jwt.SigningKey.Contains("dev-only", StringComparison.OrdinalIgnoreCase) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32))
    throw new InvalidOperationException("Jwt:SigningKey is the dev placeholder or shorter than 32 bytes. Provide a strong key via env/secret store before deploying.");
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        // SignalR передаёт access_token в query-string при WebSocket-апгрейде.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Тормозим перебор паролей / спам регистраций: 10 запросов/мин на IP для /auth/*.
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    // Анонимный публичный /vacancies: ограничиваем, чтобы ILIKE-поиск нельзя было заспамить.
    options.AddPolicy("public", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});

// Observability: трейсы (ASP.NET/HttpClient/Npgsql) + метрики. Экспорт по OTLP включаем
// только если задан endpoint — иначе телеметрия собирается, но никуда не шлётся (без шума в dev/тестах).
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("JobRadar.Api"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddSource("Npgsql");
        if (otlpEndpoint is not null) t.AddOtlpExporter();
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation();
        if (otlpEndpoint is not null) m.AddOtlpExporter();
    });

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Missing connection string 'Postgres'."));

var app = builder.Build();

// Опционально накатываем миграции на старте — для платформ без отдельного release-шага
// (напр. Railway): выставить RunMigrationsOnStartup=true. По умолчанию выключено.
if (app.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<JobRadarDbContext>().Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
    app.UseHttpsRedirection(); // токены не должны ходить по plaintext (за TLS-терминирующим прокси — no-op)
}

// Базовые security-заголовки на все ответы (defense-in-depth; OWASP API8).
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseCors(SpaCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var auth = app.MapGroup("/auth").RequireRateLimiting("auth");

auth.MapPost("/register", async (RegisterRequest request, IAuthService service, CancellationToken ct) =>
{
    if (RequestValidation.ForRegister(request) is { } problem) return problem;
    var outcome = await service.RegisterAsync(request, ct);
    return outcome.Succeeded
        ? Results.Ok(outcome.Tokens)
        : Results.Conflict(new { error = outcome.Error.ToString() });
});

auth.MapPost("/login", async (LoginRequest request, IAuthService service, CancellationToken ct) =>
{
    if (RequestValidation.ForLogin(request) is { } problem) return problem;
    var outcome = await service.LoginAsync(request, ct);
    return outcome.Succeeded ? Results.Ok(outcome.Tokens) : Results.Unauthorized();
});

auth.MapPost("/refresh", async (RefreshRequest request, IAuthService service, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken)) return Results.Unauthorized();
    var outcome = await service.RefreshAsync(request.RefreshToken, ct);
    return outcome.Succeeded ? Results.Ok(outcome.Tokens) : Results.Unauthorized();
});

auth.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
{
    id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"),
    email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email"),
    role = user.FindFirstValue(ClaimTypes.Role),
})).RequireAuthorization();

app.MapGet("/vacancies", async (
    IVacancyQueryService vacancies,
    string? market, string? level, string? stack, string? q,
    int? page, int? pageSize,
    CancellationToken ct) =>
{
    var result = await vacancies.SearchAsync(new VacancyQuery
    {
        Market = market,
        Level = level,
        Stack = stack,
        Q = q,
        Page = page ?? 1,
        PageSize = pageSize ?? 20,
    }, ct);
    return Results.Ok(result);
}).RequireRateLimiting("public");

var filters = app.MapGroup("/me/filters").RequireAuthorization();

filters.MapGet("/", async (ISavedFilterService service, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.GetUserId();
    return userId is null ? Results.Unauthorized() : Results.Ok(await service.ListAsync(userId.Value, ct));
});

filters.MapPost("/", async (CreateSavedFilterRequest request, ISavedFilterService service, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.GetUserId();
    if (userId is null) return Results.Unauthorized();
    if (RequestValidation.ForSavedFilter(request.Name, request.Market, request.Level, request.Stack, request.Q) is { } problem)
        return problem;
    var created = await service.CreateAsync(userId.Value, request, ct);
    return Results.Created($"/me/filters/{created.Id}", created);
});

filters.MapPut("/{id:int}", async (int id, UpdateSavedFilterRequest request, ISavedFilterService service, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.GetUserId();
    if (userId is null) return Results.Unauthorized();
    if (RequestValidation.ForSavedFilter(request.Name, request.Market, request.Level, request.Stack, request.Q) is { } problem)
        return problem;
    var outcome = await service.UpdateAsync(userId.Value, id, request, ct);
    return outcome.Status switch
    {
        SavedFilterUpdateStatus.Updated => Results.Ok(outcome.Filter),
        SavedFilterUpdateStatus.NotFound => Results.NotFound(),
        _ => Results.Conflict(new { error = "version_conflict" }),
    };
});

filters.MapDelete("/{id:int}", async (int id, ISavedFilterService service, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.GetUserId();
    if (userId is null) return Results.Unauthorized();
    return await service.DeleteAsync(userId.Value, id, ct) ? Results.NoContent() : Results.NotFound();
});

app.MapPost("/vacancies/{vacancyId:int}/applications", async (
    int vacancyId, CreateApplicationRequest request, IApplicationService service, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.GetUserId();
    if (userId is null) return Results.Unauthorized();
    if (RequestValidation.ForApplication(request.CoverLetter) is { } problem) return problem;
    var outcome = await service.ApplyAsync(userId.Value, vacancyId, request, ct);
    return outcome.Result switch
    {
        ApplyResult.Created => Results.Created($"/me/applications/{outcome.Application!.Id}", outcome.Application),
        ApplyResult.AlreadyApplied => Results.Conflict(new { error = "already_applied" }),
        _ => Results.NotFound(),
    };
}).RequireAuthorization();

var applications = app.MapGroup("/me/applications").RequireAuthorization();

applications.MapGet("/", async (IApplicationService service, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.GetUserId();
    return userId is null ? Results.Unauthorized() : Results.Ok(await service.ListAsync(userId.Value, ct));
});

applications.MapGet("/{id:int}", async (int id, IApplicationService service, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.GetUserId();
    if (userId is null) return Results.Unauthorized();
    var application = await service.GetAsync(userId.Value, id, ct);
    return application is null ? Results.NotFound() : Results.Ok(application);
});

applications.MapPatch("/{id:int}/status", async (
    int id, UpdateApplicationStatusRequest request, IApplicationService service, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.GetUserId();
    if (userId is null) return Results.Unauthorized();
    var outcome = await service.ChangeStatusAsync(userId.Value, id, request, ct);
    return outcome.Result switch
    {
        StatusChangeResult.Changed => Results.Ok(outcome.Application),
        StatusChangeResult.NotFound => Results.NotFound(),
        StatusChangeResult.IllegalTransition => Results.UnprocessableEntity(new { error = "illegal_transition" }),
        _ => Results.Conflict(new { error = "version_conflict" }),
    };
});

applications.MapDelete("/{id:int}", async (int id, IApplicationService service, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.GetUserId();
    if (userId is null) return Results.Unauthorized();
    return await service.DeleteAsync(userId.Value, id, ct) ? Results.NoContent() : Results.NotFound();
});

var employer = app.MapGroup("/employer")
    .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Employer)));

employer.MapPost("/vacancies", async (CreateVacancyRequest request, IEmployerService service, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.GetUserId();
    if (userId is null) return Results.Unauthorized();
    if (RequestValidation.ForEmployerVacancy(request) is { } problem) return problem;
    var created = await service.PostVacancyAsync(userId.Value, request, ct);
    return Results.Created($"/vacancies/{created.Id}", created);
});

employer.MapGet("/applications", async (IEmployerService service, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.GetUserId();
    return userId is null ? Results.Unauthorized() : Results.Ok(await service.ListApplicationsAsync(userId.Value, ct));
});

employer.MapPatch("/applications/{id:int}/status", async (
    int id, UpdateApplicationStatusRequest request, IEmployerService service,
    IHubContext<VacancyHub> hub, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.GetUserId();
    if (userId is null) return Results.Unauthorized();
    var outcome = await service.ChangeApplicationStatusAsync(userId.Value, id, request, ct);
    // Решение работодателя прилетает кандидату в реальном времени (его персональный канал).
    if (outcome.Result == StatusChangeResult.Changed && outcome.CandidateUserId is { } candidateId)
        await hub.Clients.User(candidateId.ToString()).SendAsync("ApplicationStatusChanged", outcome.Application, ct);
    return outcome.Result switch
    {
        StatusChangeResult.Changed => Results.Ok(outcome.Application),
        StatusChangeResult.NotFound => Results.NotFound(),
        StatusChangeResult.IllegalTransition => Results.UnprocessableEntity(new { error = "illegal_transition" }),
        _ => Results.Conflict(new { error = "version_conflict" }),
    };
});

app.MapHub<VacancyHub>("/hubs/vacancies");

app.Run();

public partial class Program;
