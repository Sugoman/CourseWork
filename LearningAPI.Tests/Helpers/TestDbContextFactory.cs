using LearningTrainerShared.Context;
using Microsoft.EntityFrameworkCore;

namespace LearningAPI.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ApiDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        var context = new TestApiDbContext(options);
        return context;
    }

    public static ApiDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new TestApiDbContext(options);
        return context;
    }
}

/// <summary>
/// Test DbContext that skips seed data
/// </summary>
public class TestApiDbContext : ApiDbContext
{
    public TestApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Skip seeding for tests - call base but don't seed roles
        modelBuilder.Entity<LearningTrainerShared.Models.LearningProgress>()
            .HasAlternateKey(p => new { p.UserId, p.WordId });

        modelBuilder.Entity<LearningTrainerShared.Models.Dictionary>()
            .HasMany(d => d.Words)
            .WithOne(w => w.Dictionary)
            .HasForeignKey(w => w.DictionaryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LearningTrainerShared.Models.LearningProgress>()
            .HasOne(lp => lp.User)
            .WithMany()
            .HasForeignKey(lp => lp.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LearningTrainerShared.Models.LearningProgress>()
            .HasOne(lp => lp.Word)
            .WithMany(w => w.Progress)
            .HasForeignKey(lp => lp.WordId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LearningTrainerShared.Models.LearningProgress>()
            .HasIndex(p => p.UserId);

        modelBuilder.Entity<LearningTrainerShared.Models.LearningProgress>()
            .HasIndex(p => p.WordId);

        modelBuilder.Entity<LearningTrainerShared.Models.LearningProgress>()
            .HasIndex(p => p.NextReview);

        modelBuilder.Entity<LearningTrainerShared.Models.DictionarySharing>()
            .HasOne(ds => ds.User)
            .WithMany()
            .HasForeignKey(ds => ds.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LearningTrainerShared.Models.DictionarySharing>()
            .HasOne(ds => ds.Dictionary)
            .WithMany()
            .HasForeignKey(ds => ds.DictionaryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LearningTrainerShared.Models.RuleSharing>()
            .HasOne(rs => rs.User)
            .WithMany()
            .HasForeignKey(rs => rs.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LearningTrainerShared.Models.RuleSharing>()
            .HasOne(rs => rs.Rule)
            .WithMany()
            .HasForeignKey(rs => rs.RuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
