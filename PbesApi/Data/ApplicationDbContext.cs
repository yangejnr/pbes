using Microsoft.EntityFrameworkCore;
using PbesApi.Models;

namespace PbesApi.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Officer> Officers => Set<Officer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Officer>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.HasIndex(o => o.ServiceNumber).IsUnique();

            entity.Property(o => o.ServiceNumber).IsRequired();
            entity.Property(o => o.Email).IsRequired();
            entity.Property(o => o.PasswordHash).IsRequired();
            entity.Property(o => o.Role).IsRequired();
        });
    }
}
