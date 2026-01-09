using LearningTrainer.Context;
using Microsoft.EntityFrameworkCore;

namespace LearningAPI.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ApiDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ApiDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static ApiDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new ApiDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
