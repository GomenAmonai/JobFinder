using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using JobRadar.Api.Hubs;
using JobRadar.Api.Ingestion;
using JobRadar.Application.Auth;
using JobRadar.Application.Ingestion;
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
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/auth/register", async (RegisterRequest request, IAuthService auth, CancellationToken ct) =>
{
    var outcome = await auth.RegisterAsync(request, ct);
    return outcome.Succeeded
        ? Results.Ok(outcome.Tokens)
        : Results.Conflict(new { error = outcome.Error.ToString() });
});

app.MapPost("/auth/login", async (LoginRequest request, IAuthService auth, CancellationToken ct) =>
{
    var outcome = await auth.LoginAsync(request, ct);
    return outcome.Succeeded ? Results.Ok(outcome.Tokens) : Results.Unauthorized();
});

app.MapPost("/auth/refresh", async (RefreshRequest request, IAuthService auth, CancellationToken ct) =>
{
    var outcome = await auth.RefreshAsync(request.RefreshToken, ct);
    return outcome.Succeeded ? Results.Ok(outcome.Tokens) : Results.Unauthorized();
});

app.MapGet("/auth/me", (ClaimsPrincipal user) => Results.Ok(new
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

app.MapHub<VacancyHub>("/hubs/vacancies");

app.Run();

public partial class Program;
