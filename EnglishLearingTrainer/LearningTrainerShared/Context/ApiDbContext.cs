using EnglishLearningTrainer.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnglishLearningTrainer.Context
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
