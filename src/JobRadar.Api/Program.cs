using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using JobRadar.Api.Hubs;
using JobRadar.Api.Ingestion;
using JobRadar.Api;
using JobRadar.Application.Auth;
using JobRadar.Application.Ingestion;
using JobRadar.Application.SavedFilters;
using JobRadar.Application.Vacancies;
using JobRadar.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

const string SpaCorsPolicy = "spa";
builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy => policy
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // нужно для SignalR

builder.Services.AddOpenApi();
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection(KafkaSettings.SectionName));
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHostedService<VacancyChangedConsumer>();

var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Missing 'Jwt' configuration.");
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
});

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Missing connection string 'Postgres'."));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

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
    var outcome = await service.RefreshAsync(request.RefreshToken, ct);
    return outcome.Succeeded ? Results.Ok(outcome.Tokens) : Results.Unauthorized();
});

auth.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
{
    id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"),
    email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email"),
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
});

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

app.MapHub<VacancyHub>("/hubs/vacancies");

app.Run();

public partial class Program;
