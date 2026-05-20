
using System;
using ChartEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace ChartEngine.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.8")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("ChartEngine.Domain.Entities.AggregationTree", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("ComputedAt")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("DatasetId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("TreeJson")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("DatasetId")
                        .IsUnique();

                    b.ToTable("AggregationTrees");
                });

            modelBuilder.Entity("ChartEngine.Domain.Entities.Dataset", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("ErrorMessage")
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.Property<string>("FileName")
                        .IsRequired()
                        .HasMaxLength(512)
                        .HasColumnType("nvarchar(512)");

                    b.Property<long>("FileSizeBytes")
                        .HasColumnType("bigint");

                    b.Property<DateTime?>("ProcessedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<string>("StoragePath")
                        .IsRequired()
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.Property<int>("TotalRows")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("Datasets");
                });

            modelBuilder.Entity("ChartEngine.Domain.Entities.DatasetColumn", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<int?>("Cardinality")
                        .HasColumnType("int");

                    b.Property<string>("ColumnName")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<Guid>("DatasetId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("RejectionReason")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<int>("SortOrder")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("DatasetId");

                    b.ToTable("DatasetColumns");
                });

            modelBuilder.Entity("ChartEngine.Domain.Entities.DatasetConfig", b =>
                {
                    b.Property<Guid>("DatasetId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("FallbackTopValues")
                        .HasColumnType("int");

                    b.Property<int>("MaxDimCardinality")
                        .HasColumnType("int");

                    b.Property<int>("MaxHierarchyDepth")
                        .HasColumnType("int");

                    b.HasKey("DatasetId");

                    b.ToTable("DatasetConfigs");
                });

            modelBuilder.Entity("ChartEngine.Domain.Entities.AggregationTree", b =>
                {
                    b.HasOne("ChartEngine.Domain.Entities.Dataset", null)
                        .WithOne()
                        .HasForeignKey("ChartEngine.Domain.Entities.AggregationTree", "DatasetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ChartEngine.Domain.Entities.DatasetColumn", b =>
                {
                    b.HasOne("ChartEngine.Domain.Entities.Dataset", null)
                        .WithMany()
                        .HasForeignKey("DatasetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ChartEngine.Domain.Entities.DatasetConfig", b =>
                {
                    b.HasOne("ChartEngine.Domain.Entities.Dataset", "Dataset")
                        .WithOne("Config")
                        .HasForeignKey("ChartEngine.Domain.Entities.DatasetConfig", "DatasetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}

