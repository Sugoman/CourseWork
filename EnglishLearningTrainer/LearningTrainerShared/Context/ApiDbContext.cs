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



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LearningProgress>()
                .HasIndex(p => p.UserId);

            modelBuilder.Entity<LearningProgress>()
                .HasIndex(p => p.WordId);

            modelBuilder.Entity<LearningProgress>()
                .HasIndex(p => p.NextReview);
        }
    }
}
