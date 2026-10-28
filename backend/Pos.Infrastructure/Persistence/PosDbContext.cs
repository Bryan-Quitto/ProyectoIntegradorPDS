using Microsoft.EntityFrameworkCore;
using Pos.Infrastructure.Persistence.Entities;
using System.Security.Cryptography;
using System.Text;

namespace Pos.Infrastructure.Persistence;

public class PosDbContext : DbContext
{
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<SaleAuditRecord> SaleAuditRecords => Set<SaleAuditRecord>();
    public DbSet<LocalOperator> LocalOperators => Set<LocalOperator>();

    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).IsRequired().HasMaxLength(128);
        });

        modelBuilder.Entity<SaleAuditRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderId).IsUnique();
        });

        modelBuilder.Entity<LocalOperator>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // Seed initial admin cashier (RNF-09: hashed password for local operators)
        string adminPasswordHash = HashPassword("admin123");
        modelBuilder.Entity<LocalOperator>().HasData(new LocalOperator
        {
            Id = 1,
            Username = "admin",
            PasswordHash = adminPasswordHash,
            FullName = "Operador Principal",
            Role = "Administrator",
            IsActive = true
        });
    }

    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
