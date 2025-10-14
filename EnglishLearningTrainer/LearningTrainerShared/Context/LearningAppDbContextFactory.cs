using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

// Убедись, что namespace тот же, что и у твоего DbContext
namespace EnglishLearningTrainer.Context
{
    // Этот класс говорит 'Add-Migration', как создавать DbContext
    public class LearningAppDbContextFactory : IDesignTimeDbContextFactory<ApiDbContext>
    {
        public ApiDbContext CreateDbContext(string[] args)
        {
            // Хитрый способ найти 'appsettings.json' из твоего API-проекта.
            // Он ищет папку 'LearningAPI' на один уровень "вверх и вбок".
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "LearningAPI")) 
                .AddJsonFile("appsettings.json")
                .Build();

            // Читаем ту же строку подключения, что и API
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<ApiDbContext>();

            // Мы ПРИНУДИТЕЛЬНО говорим мигратору: используй SQL Server
            optionsBuilder.UseSqlServer(connectionString);

            // Создаем контекст с этими 'options'
            return new ApiDbContext(optionsBuilder.Options);
        }
    }
}