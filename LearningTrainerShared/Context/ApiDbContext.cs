using LearningTrainerShared.Models;
using Microsoft.EntityFrameworkCore;

namespace LearningTrainer.Context
{
    public class ApiDbContext : DbContext
    {
        public ApiDbContext(DbContextOptions<ApiDbContext> options)
        : base(options)
        {
        }


        public DbSet<User> Users { get; set; }
        public DbSet<Dictionary> Dictionaries { get; set; }
        public DbSet<Word> Words { get; set; }
        public DbSet<Rule> Rules { get; set; }
        public DbSet<LearningProgress> LearningProgresses { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<DictionarySharing> DictionarySharings { get; set; } = null!;
        public DbSet<RuleSharing> RuleSharings { get; set; } = null!;
        
        // Marketplace entities
        public DbSet<Comment> Comments { get; set; } = null!;
        public DbSet<Download> Downloads { get; set; } = null!;



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseCollation("utf8mb4_unicode_ci");
            // Seed roles including new User role
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "Teacher" },
                new Role { Id = 3, Name = "Student" },
                new Role { Id = 4, Name = "User" }
            );

            modelBuilder.Entity<LearningProgress>()
                .HasAlternateKey(p => new { p.UserId, p.WordId });

            modelBuilder.Entity<Dictionary>()
                .HasMany(d => d.Words)
                .WithOne(w => w.Dictionary)
                .HasForeignKey(w => w.DictionaryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LearningProgress>()
                .HasOne(lp => lp.User)
                .WithMany()
                .HasForeignKey(lp => lp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LearningProgress>()
                .HasOne(lp => lp.Word)
                .WithMany(w => w.Progress)
                .HasForeignKey(lp => lp.WordId)
                .OnDelete(DeleteBehavior.Restrict); 

            modelBuilder.Entity<LearningProgress>()
                .HasIndex(p => p.UserId);

            modelBuilder.Entity<LearningProgress>()
                .HasIndex(p => p.WordId);

            modelBuilder.Entity<LearningProgress>()
                .HasIndex(p => p.NextReview);

            modelBuilder.Entity<DictionarySharing>()
                .HasOne(ds => ds.User)
                .WithMany()
                .HasForeignKey(ds => ds.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DictionarySharing>()
                .HasOne(ds => ds.Dictionary)
                .WithMany()
                .HasForeignKey(ds => ds.DictionaryId)
                .OnDelete(DeleteBehavior.Restrict);

            // RuleSharing (Правило -> Ученик)
            modelBuilder.Entity<RuleSharing>()
                .HasOne(rs => rs.User)
                .WithMany()
                .HasForeignKey(rs => rs.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RuleSharing>()
                .HasOne(rs => rs.Rule)
                .WithMany()
                .HasForeignKey(rs => rs.RuleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
