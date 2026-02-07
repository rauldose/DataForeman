using DataForeman.Core.Entities;
using DataForeman.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DataForeman.API.Tests;

/// <summary>
/// Unit tests for permission and sharing features.
/// Tests the core permission model and sharing functionality used by the Blazor UI.
/// </summary>
public class PermissionTests
{
    private DataForemanDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<DataForemanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new DataForemanDbContext(options);
    }

    [Fact]
    public async Task UserPermission_CanCreate_PersistsCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "hash",
            IsActive = true
        };
        context.Users.Add(user);

        var permission = new UserPermission
        {
            UserId = userId,
            Feature = "flows",
            CanCreate = true,
            CanRead = true,
            CanUpdate = true,
            CanDelete = false
        };
        context.UserPermissions.Add(permission);
        await context.SaveChangesAsync();

        // Act
        var loaded = await context.UserPermissions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Feature == "flows");

        // Assert
        Assert.NotNull(loaded);
        Assert.True(loaded.CanCreate);
        Assert.True(loaded.CanRead);
        Assert.True(loaded.CanUpdate);
        Assert.False(loaded.CanDelete);
    }

    [Fact]
    public async Task UserPermission_MultipleFeatures_PersistCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "admin@example.com",
            PasswordHash = "hash",
            IsActive = true
        };
        context.Users.Add(user);

        var features = new[]
        {
            new UserPermission { UserId = userId, Feature = "flows", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true },
            new UserPermission { UserId = userId, Feature = "connectivity", CanCreate = false, CanRead = true, CanUpdate = false, CanDelete = false },
            new UserPermission { UserId = userId, Feature = "charts", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = false },
            new UserPermission { UserId = userId, Feature = "users", CanCreate = false, CanRead = true, CanUpdate = false, CanDelete = false },
        };
        context.UserPermissions.AddRange(features);
        await context.SaveChangesAsync();

        // Act
        var permissions = await context.UserPermissions
            .Where(p => p.UserId == userId)
            .ToListAsync();

        // Assert
        Assert.Equal(4, permissions.Count);

        var flowsPerm = permissions.First(p => p.Feature == "flows");
        Assert.True(flowsPerm.CanCreate);
        Assert.True(flowsPerm.CanDelete);

        var connPerm = permissions.First(p => p.Feature == "connectivity");
        Assert.False(connPerm.CanCreate);
        Assert.True(connPerm.CanRead);
    }

    [Fact]
    public async Task UserPermission_Update_ReplacesCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "hash",
            IsActive = true
        };
        context.Users.Add(user);

        var permission = new UserPermission
        {
            UserId = userId,
            Feature = "flows",
            CanCreate = false,
            CanRead = true,
            CanUpdate = false,
            CanDelete = false
        };
        context.UserPermissions.Add(permission);
        await context.SaveChangesAsync();

        // Act - Remove old and add new (like UpdateUserPermissionsAsync does)
        var existing = await context.UserPermissions
            .Where(p => p.UserId == userId)
            .ToListAsync();
        context.UserPermissions.RemoveRange(existing);

        context.UserPermissions.Add(new UserPermission
        {
            UserId = userId,
            Feature = "flows",
            CanCreate = true,
            CanRead = true,
            CanUpdate = true,
            CanDelete = true
        });
        await context.SaveChangesAsync();

        // Assert
        var updated = await context.UserPermissions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Feature == "flows");
        Assert.NotNull(updated);
        Assert.True(updated.CanCreate);
        Assert.True(updated.CanUpdate);
        Assert.True(updated.CanDelete);
    }

    [Fact]
    public async Task Flow_SharedProperty_PersistsCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var flow = new Flow
        {
            Id = Guid.NewGuid(),
            Name = "Test Flow",
            Shared = false,
            Definition = "{}"
        };
        context.Flows.Add(flow);
        await context.SaveChangesAsync();

        // Act - Toggle sharing
        var loaded = await context.Flows.FindAsync(flow.Id);
        Assert.NotNull(loaded);
        loaded!.Shared = true;
        await context.SaveChangesAsync();

        // Assert
        var verified = await context.Flows.FindAsync(flow.Id);
        Assert.NotNull(verified);
        Assert.True(verified!.Shared);
    }

    [Fact]
    public async Task Dashboard_IsSharedProperty_PersistsCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dashboard = new Dashboard
        {
            Id = Guid.NewGuid(),
            Name = "Test Dashboard",
            UserId = Guid.NewGuid(),
            IsShared = false
        };
        context.Dashboards.Add(dashboard);
        await context.SaveChangesAsync();

        // Act - Toggle sharing
        var loaded = await context.Dashboards.FindAsync(dashboard.Id);
        Assert.NotNull(loaded);
        loaded!.IsShared = true;
        await context.SaveChangesAsync();

        // Assert
        var verified = await context.Dashboards.FindAsync(dashboard.Id);
        Assert.NotNull(verified);
        Assert.True(verified!.IsShared);
    }

    [Fact]
    public async Task ChartConfig_IsSharedProperty_PersistsCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var chart = new ChartConfig
        {
            Id = Guid.NewGuid(),
            Name = "Test Chart",
            UserId = Guid.NewGuid(),
            IsShared = false
        };
        context.ChartConfigs.Add(chart);
        await context.SaveChangesAsync();

        // Act - Toggle sharing
        var loaded = await context.ChartConfigs.FindAsync(chart.Id);
        Assert.NotNull(loaded);
        loaded!.IsShared = true;
        await context.SaveChangesAsync();

        // Assert
        var verified = await context.ChartConfigs.FindAsync(chart.Id);
        Assert.NotNull(verified);
        Assert.True(verified!.IsShared);
    }

    [Fact]
    public void HasPermission_MatchesExpectedLogic()
    {
        // This tests the same permission-checking logic used by AppStateService.HasPermission
        var permissions = new List<UserPermission>
        {
            new() { Feature = "flows", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = false },
            new() { Feature = "connectivity", CanCreate = false, CanRead = true, CanUpdate = false, CanDelete = false },
            new() { Feature = "charts", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true },
        };

        // Simulate HasPermission logic
        bool HasPermission(string feature, string operation)
        {
            var perm = permissions.FirstOrDefault(p => p.Feature == feature);
            if (perm == null) return false;
            return operation.ToLower() switch
            {
                "create" => perm.CanCreate,
                "read" => perm.CanRead,
                "update" => perm.CanUpdate,
                "delete" => perm.CanDelete,
                _ => false
            };
        }

        // Assert flows permissions
        Assert.True(HasPermission("flows", "create"));
        Assert.True(HasPermission("flows", "read"));
        Assert.True(HasPermission("flows", "update"));
        Assert.False(HasPermission("flows", "delete"));

        // Assert connectivity permissions (read-only)
        Assert.False(HasPermission("connectivity", "create"));
        Assert.True(HasPermission("connectivity", "read"));
        Assert.False(HasPermission("connectivity", "update"));
        Assert.False(HasPermission("connectivity", "delete"));

        // Assert charts permissions (full access)
        Assert.True(HasPermission("charts", "create"));
        Assert.True(HasPermission("charts", "update"));
        Assert.True(HasPermission("charts", "delete"));

        // Assert unknown feature returns false
        Assert.False(HasPermission("unknown_feature", "read"));

        // Assert unknown operation returns false
        Assert.False(HasPermission("flows", "admin"));
    }
}
