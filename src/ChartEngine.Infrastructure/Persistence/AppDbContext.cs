namespace ChartEngine.Infrastructure.Persistence;

using ChartEngine.Domain.Entities;
using ChartEngine.Domain.Enums;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<DatasetColumn> DatasetColumns => Set<DatasetColumn>();
    public DbSet<DatasetConfig> DatasetConfigs => Set<DatasetConfig>();
    public DbSet<AggregationTree> AggregationTrees => Set<AggregationTree>();

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

        modelBuilder.Entity<DatasetColumn>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ColumnName).IsRequired().HasMaxLength(256);
            entity.Property(c => c.Role).HasConversion<string>().HasMaxLength(20);
            entity.Property(c => c.RejectionReason).HasMaxLength(100);

            // When a Dataset is deleted, all its columns are deleted too
            entity.HasOne<Dataset>()
                  .WithMany()
                  .HasForeignKey(c => c.DatasetId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DatasetConfig>(entity =>
        {
            entity.HasKey(c => c.DatasetId);  // one-to-one: DatasetId IS the primary key

            entity.HasOne<Dataset>()
                  .WithOne()
                  .HasForeignKey<DatasetConfig>(c => c.DatasetId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AggregationTree>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.HasIndex(t => t.DatasetId).IsUnique();  // one tree per dataset

            entity.Property(t => t.TreeJson).IsRequired();  // no max length — can be very large

            entity.HasOne<Dataset>()
                  .WithOne()
                  .HasForeignKey<AggregationTree>(t => t.DatasetId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
