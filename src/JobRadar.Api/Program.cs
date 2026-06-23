using System.Text.Json.Serialization;
using JobRadar.Api.Hubs;
using JobRadar.Api.Ingestion;
using JobRadar.Application.Ingestion;
using JobRadar.Application.Vacancies;
using JobRadar.Infrastructure;
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

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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
