namespace ChartEngine.Infrastructure.Persistence;

using ChartEngine.Domain.Entities;
using ChartEngine.Domain.Enums;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Dataset> Datasets => Set<Dataset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tell EF Core how to map the Dataset entity to the Datasets table
        modelBuilder.Entity<Dataset>(entity =>
        {
            entity.HasKey(d => d.Id);

            entity.Property(d => d.FileName)
                .IsRequired()
                .HasMaxLength(512);

            entity.Property(d => d.StoragePath)
                .IsRequired()
                .HasMaxLength(1024);

            // Store the enum as a string in the DB, not a number
            // "Ready" is readable; 2 is not
            entity.Property(d => d.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(d => d.ErrorMessage)
                .HasMaxLength(4000);
        });
    }
}
