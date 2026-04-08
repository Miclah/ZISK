using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;

namespace ZISK.Tests;

public class DbSchemaTests
{
    private static DbContextOptions<ApplicationDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("SchemaTestDb_" + Guid.NewGuid())
            .Options;

    private static IEnumerable<string> IndexNames<T>(ApplicationDbContext ctx)
    {
        var entityType = ctx.Model.FindEntityType(typeof(T))!;
        return entityType.GetIndexes().Select(i => i.GetDatabaseName() ?? string.Join("_", i.Properties.Select(p => p.Name)));
    }

    [Fact]
    public void ChildProfile_HasIndex_OnEmail()
    {
        using var ctx = new ApplicationDbContext(CreateOptions());
        var indexes = IndexNames<ChildProfile>(ctx);
        Assert.Contains(indexes, n => n!.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChildProfile_HasUniqueIndex_OnUserId()
    {
        using var ctx = new ApplicationDbContext(CreateOptions());
        var entityType = ctx.Model.FindEntityType(typeof(ChildProfile))!;
        var userIdIndex = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "UserId"));
        Assert.NotNull(userIdIndex);
        Assert.True(userIdIndex.IsUnique);
    }

    [Fact]
    public void ChildProfile_HasFk_ToApplicationUser_ViaUserId()
    {
        using var ctx = new ApplicationDbContext(CreateOptions());
        var entityType = ctx.Model.FindEntityType(typeof(ChildProfile))!;
        var fk = entityType.GetForeignKeys()
            .FirstOrDefault(f => f.Properties.Any(p => p.Name == "UserId"));
        Assert.NotNull(fk);
        Assert.Equal(DeleteBehavior.SetNull, fk.DeleteBehavior);
    }

    [Fact]
    public void AbsenceRequest_HasIndex_OnStatus()
    {
        using var ctx = new ApplicationDbContext(CreateOptions());
        var indexes = IndexNames<AbsenceRequest>(ctx);
        Assert.Contains(indexes, n => n!.Contains("Status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Document_HasFk_ToApplicationUser_ViaUploadedByUserId()
    {
        using var ctx = new ApplicationDbContext(CreateOptions());
        var entityType = ctx.Model.FindEntityType(typeof(Document))!;
        var fk = entityType.GetForeignKeys()
            .FirstOrDefault(f => f.Properties.Any(p => p.Name == "UploadedByUserId"));
        Assert.NotNull(fk);
        Assert.Equal(DeleteBehavior.SetNull, fk.DeleteBehavior);
    }

    [Fact]
    public void ApplicationUser_HasUniqueIndex_OnPhoneNumber()
    {
        using var ctx = new ApplicationDbContext(CreateOptions());
        var entityType = ctx.Model.FindEntityType(typeof(ApplicationUser))!;
        var idx = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "PhoneNumber"));
        Assert.NotNull(idx);
        Assert.True(idx.IsUnique);
    }

    [Fact]
    public void ApplicationUser_HasUniqueIndex_OnRodneCislo()
    {
        using var ctx = new ApplicationDbContext(CreateOptions());
        var entityType = ctx.Model.FindEntityType(typeof(ApplicationUser))!;
        var idx = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "RodneCislo"));
        Assert.NotNull(idx);
        Assert.True(idx.IsUnique);
    }

    [Fact]
    public void ApplicationUser_PhoneNumber_HasMaxLength20()
    {
        using var ctx = new ApplicationDbContext(CreateOptions());
        var entityType = ctx.Model.FindEntityType(typeof(ApplicationUser))!;
        var prop = entityType.GetProperties().First(p => p.Name == "PhoneNumber");
        Assert.Equal(20, prop.GetMaxLength());
    }

    [Fact]
    public void CoachTeam_HasFk_ToTeam_WithNavigationOnTeam()
    {
        using var ctx = new ApplicationDbContext(CreateOptions());
        var teamEntityType = ctx.Model.FindEntityType(typeof(Team))!;
        var coachesNav = teamEntityType.GetNavigations()
            .FirstOrDefault(n => n.Name == "Coaches");
        Assert.NotNull(coachesNav);
    }

    [Fact]
    public void CoachTeam_HasFk_ToUser_WithNavigationOnApplicationUser()
    {
        using var ctx = new ApplicationDbContext(CreateOptions());
        var userEntityType = ctx.Model.FindEntityType(typeof(ApplicationUser))!;
        var coachTeamsNav = userEntityType.GetNavigations()
            .FirstOrDefault(n => n.Name == "CoachTeams");
        Assert.NotNull(coachTeamsNav);
    }

    [Fact]
    public void AttendanceRecord_ChildFk_IsRestrict()
    {
        using var ctx = new ApplicationDbContext(CreateOptions());
        var entityType = ctx.Model.FindEntityType(typeof(AttendanceRecord))!;
        var fk = entityType.GetForeignKeys()
            .First(f => f.Properties.Any(p => p.Name == "ChildId"));
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Fact]
    public void TrainingEvent_TeamFk_IsCascade()
    {
        using var ctx = new ApplicationDbContext(CreateOptions());
        var entityType = ctx.Model.FindEntityType(typeof(TrainingEvent))!;
        var fk = entityType.GetForeignKeys()
            .First(f => f.Properties.Any(p => p.Name == "TeamId"));
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
    }

    [Fact]
    public void ChildProfile_TeamFk_IsSetNull()
    {
        using var ctx = new ApplicationDbContext(CreateOptions());
        var entityType = ctx.Model.FindEntityType(typeof(ChildProfile))!;
        var fk = entityType.GetForeignKeys()
            .First(f => f.Properties.Any(p => p.Name == "TeamId"));
        Assert.Equal(DeleteBehavior.SetNull, fk.DeleteBehavior);
    }
}
