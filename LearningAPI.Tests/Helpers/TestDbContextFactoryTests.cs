using FluentAssertions;
using LearningAPI.Tests.Helpers;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using Xunit;

namespace LearningAPI.Tests.Helpers;

public class TestDbContextFactoryTests
{
    [Fact]
    public void CreateInMemoryContext_ReturnsValidContext()
    {
        // Act
        using var context = TestDbContextFactory.CreateInMemoryContext();

        // Assert
        context.Should().NotBeNull();
        context.Should().BeOfType<TestApiDbContext>();
    }

    [Fact]
    public void CreateInMemoryContext_CreatesIsolatedDatabase()
    {
        // Act
        using var context1 = TestDbContextFactory.CreateInMemoryContext();
        using var context2 = TestDbContextFactory.CreateInMemoryContext();

        // Add data to context1
        context1.Roles.Add(new Role { Id = 100, Name = "TestRole1" });
        context1.SaveChanges();

        // Assert - context2 should not have the data from context1 (isolated)
        context2.Roles.Any(r => r.Id == 100).Should().BeFalse();
    }

    [Fact]
    public void CreateInMemoryContext_WithDbName_UsesSameName()
    {
        // Arrange
        var dbName = "SharedTestDb_" + Guid.NewGuid().ToString();

        // Act
        using var context1 = TestDbContextFactory.CreateInMemoryContext(dbName);
        context1.Roles.Add(new Role { Id = 200, Name = "SharedRole" });
        context1.SaveChanges();
        
        using var context2 = TestDbContextFactory.CreateInMemoryContext(dbName);

        // Assert - both contexts should share the same database
        context2.Roles.Any(r => r.Id == 200).Should().BeTrue();
    }

    [Fact]
    public void CreateInMemoryContext_HasAllDbSets()
    {
        // Act
        using var context = TestDbContextFactory.CreateInMemoryContext();

        // Assert
        context.Users.Should().NotBeNull();
        context.Roles.Should().NotBeNull();
        context.Dictionaries.Should().NotBeNull();
        context.Words.Should().NotBeNull();
        context.Rules.Should().NotBeNull();
        context.LearningProgresses.Should().NotBeNull();
        context.DictionarySharings.Should().NotBeNull();
        context.RuleSharings.Should().NotBeNull();
        context.Comments.Should().NotBeNull();
        context.Downloads.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateInMemoryContext_CanSaveAndRetrieveData()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var role = TestDataSeeder.CreateTeacherRole();

        // Act
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        // Assert
        var retrievedRole = await context.Roles.FirstOrDefaultAsync(r => r.Id == 1);
        retrievedRole.Should().NotBeNull();
        retrievedRole!.Name.Should().Be("Teacher");
    }
}
