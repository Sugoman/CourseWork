using EnglishLearningTrainer.Models; // Убедись, что Models тут
using Microsoft.EntityFrameworkCore;

namespace EnglishLearningTrainer.Context
{
    public class LocalDbContext : DbContext
    {
        public LocalDbContext()
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Это и есть наш "автономный режим"
                optionsBuilder.UseSqlite("Data Source=EnglishLearning.db");
            }
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Dictionary> Dictionaries { get; set; }
        public DbSet<Word> Words { get; set; }
        public DbSet<Rule> Rules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Конфигурация связей
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