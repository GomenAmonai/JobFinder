using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence;

public class JobRadarDbContext(DbContextOptions<JobRadarDbContext> options) : DbContext(options)
{
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SavedFilter> SavedFilters => Set<SavedFilter>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();

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
        v.Property(x => x.DedupKey).HasMaxLength(820); // company(300)+'|'+title(500) после нормализации
        v.HasIndex(x => x.DedupKey);
        v.HasIndex(x => x.PostedByUserId); // автор нативной вакансии; фильтрация откликов работодателя

        var u = b.Entity<User>();
        u.HasKey(x => x.Id);
        u.HasIndex(x => x.Email).IsUnique();
        u.Property(x => x.Email).HasMaxLength(256);
        u.Property(x => x.DisplayName).HasMaxLength(100);
        // DB-дефолт бэкфилит существующие строки на Candidate при миграции; на вставке роль ставит AuthService.
        u.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).HasDefaultValue(UserRole.Candidate);

        var rt = b.Entity<RefreshToken>();
        rt.HasKey(x => x.Id);
        rt.HasIndex(x => x.TokenHash).IsUnique();
        rt.Property(x => x.TokenHash).HasMaxLength(64); // SHA-256 в hex
        rt.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        var sf = b.Entity<SavedFilter>();
        sf.HasKey(x => x.Id);
        sf.HasIndex(x => x.UserId);
        sf.Property(x => x.Name).HasMaxLength(100);
        sf.Property(x => x.Market).HasMaxLength(50);
        sf.Property(x => x.Level).HasMaxLength(30);
        sf.Property(x => x.Stack).HasMaxLength(50);
        sf.Property(x => x.Q).HasMaxLength(100);
        sf.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        // xmin как concurrency-token — здесь он реально задействован (интерактивные правки).
        sf.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        var ap = b.Entity<JobApplication>();
        ap.HasKey(x => x.Id);
        // Откликнуться на одну вакансию можно только раз — этим держится идемпотентность.
        ap.HasIndex(x => new { x.UserId, x.VacancyId }).IsUnique();
        ap.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        ap.Property(x => x.CoverLetter).HasMaxLength(5000);
        ap.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        ap.HasOne(x => x.Vacancy).WithMany().HasForeignKey(x => x.VacancyId).OnDelete(DeleteBehavior.Cascade);
        // xmin как concurrency-token — интерактивная смена статуса под optimistic concurrency.
        ap.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
