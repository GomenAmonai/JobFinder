using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence;

public class JobRadarDbContext(DbContextOptions<JobRadarDbContext> options) : DbContext(options)
{
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var v = b.Entity<Vacancy>();
        v.HasKey(x => x.Id);
        // Идемпотентность приёма держится на этом уникальном индексе.
        v.HasIndex(x => new { x.Source, x.ExternalId }).IsUnique();
        // Optimistic concurrency через системный столбец Postgres xmin (без отдельной колонки):
        // для отслеживаемых обновлений (SaveChanges) EF включает его в WHERE и ловит
        // конкурентное изменение строки (DbUpdateConcurrencyException). Путь приёма
        // использует атомарный ExecuteUpdate и токен не задействует — он для
        // интерактивных правок (Phase 3). Конвенция Npgsql маппит это на столбец xmin.
        v.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        v.Property(x => x.Source).HasMaxLength(50);
        v.Property(x => x.ExternalId).HasMaxLength(200);
        v.Property(x => x.Title).HasMaxLength(500);
        v.Property(x => x.Company).HasMaxLength(300);
        v.Property(x => x.Market).HasMaxLength(50);
        v.Property(x => x.Level).HasMaxLength(30);
        v.Property(x => x.Stack).HasMaxLength(50);
        v.Property(x => x.SalaryCurrency).HasMaxLength(10);

        var u = b.Entity<User>();
        u.HasKey(x => x.Id);
        u.HasIndex(x => x.Email).IsUnique();
        u.Property(x => x.Email).HasMaxLength(256);
        u.Property(x => x.DisplayName).HasMaxLength(100);

        var rt = b.Entity<RefreshToken>();
        rt.HasKey(x => x.Id);
        rt.HasIndex(x => x.TokenHash).IsUnique();
        rt.Property(x => x.TokenHash).HasMaxLength(64); // SHA-256 в hex
        rt.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
