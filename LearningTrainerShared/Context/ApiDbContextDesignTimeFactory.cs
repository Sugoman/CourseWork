using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LearningTrainerShared.Context;

/// <summary>
/// Фабрика для создания ApiDbContext во время design-time операций (dotnet ef migrations / database update).
/// Читает connection string из переменной окружения или использует fallback для локальной разработки.
/// </summary>
public class ApiDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApiDbContext>
{
    public ApiDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost;Database=LearningLanguages;Trusted_Connection=True;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<ApiDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApiDbContext(optionsBuilder.Options);
    }
}
