using GoogleIntegrationService.Domain;
using Microsoft.EntityFrameworkCore;

namespace GoogleIntegrationService.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(u => u.Id);
            // Email is nullable: Google doesn't always expose it, and a unique index treats
            // multiple NULLs as distinct, so several emailless users can coexist.
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.UserName).IsUnique();
            entity.Property(u => u.UserName).IsRequired();
            entity.Property(u => u.GoogleToken).IsRequired();
        });
    }
}
