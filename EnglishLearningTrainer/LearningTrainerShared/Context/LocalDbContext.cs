using LearningTrainerShared.Models;
using Microsoft.EntityFrameworkCore;

namespace LearningTrainer.Context
{
    public class LocalDbContext : DbContext
    {
        private readonly string _dbName;

        public LocalDbContext(string userLogin = null)
        {
            _dbName = string.IsNullOrEmpty(userLogin)
                ? "EnglishLearning.db"
                : $"{userLogin}_EnglishLearning.db";
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite($"Data Source={_dbName}");
            }
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Dictionary> Dictionaries { get; set; }
        public DbSet<Word> Words { get; set; }
        public DbSet<Rule> Rules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Dictionary>()
                .HasMany(d => d.Words)
                .WithOne(w => w.Dictionary)
                .HasForeignKey(w => w.DictionaryId);

            modelBuilder.Entity<LearningProgress>()
                .HasOne(lp => lp.User)
                .WithMany()
                .HasForeignKey(lp => lp.UserId);

            modelBuilder.Entity<LearningProgress>()
                .HasOne(lp => lp.Word)
                .WithMany()
                .HasForeignKey(lp => lp.WordId);
        }
    }
}