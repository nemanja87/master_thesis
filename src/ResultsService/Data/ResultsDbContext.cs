using Microsoft.EntityFrameworkCore;
using ResultsService.Models;

namespace ResultsService.Data;

public sealed class ResultsDbContext : DbContext
{
    public ResultsDbContext(DbContextOptions<ResultsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Run> Runs => Set<Run>();
    public DbSet<Metric> Metrics => Set<Metric>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Run>(entity =>
        {
            entity.ToTable("runs");
            entity.HasIndex(run => run.Name);
            entity.Property(run => run.ConfigurationJson).HasColumnType("jsonb");

            entity.HasMany(run => run.Metrics)
                .WithOne(metric => metric.Run!)
                .HasForeignKey(metric => metric.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Metric>(entity =>
        {
            entity.ToTable("metrics");
            entity.HasIndex(metric => new { metric.RunId, metric.Name }).IsUnique();
        });
    }
}
