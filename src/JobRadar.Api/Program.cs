using JobRadar.Infrastructure;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Missing connection string 'Postgres'."));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Phase 1: минимальное чтение, чтобы наблюдать результат приёма.
// Полноценные фильтры/пагинация — Phase 2.
app.MapGet("/vacancies", async (JobRadarDbContext db, CancellationToken ct) =>
    await db.Vacancies
        .OrderByDescending(v => v.PublishedAt)
        .Take(50)
        .ToListAsync(ct));

app.Run();

public partial class Program;
