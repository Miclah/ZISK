using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Shared.Localization;

namespace ZISK.Services;

public class ChildUpgradeWorker
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SmtpEmailSender _emailSender;
    private readonly IAuditService _auditService;
    private readonly ILogger<ChildUpgradeWorker> _logger;
    private readonly ICurrentLanguage _currentLanguage;

    public ChildUpgradeWorker(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SmtpEmailSender emailSender,
        IAuditService auditService,
        ILogger<ChildUpgradeWorker> logger,
        ICurrentLanguage currentLanguage)
    {
        _context = context;
        _userManager = userManager;
        _emailSender = emailSender;
        _auditService = auditService;
        _logger = logger;
        _currentLanguage = currentLanguage;
    }

    public async Task UpgradeSingleAsync(string userId, ClaimsPrincipal? caller = null, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException();

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains("Child"))
            throw new InvalidOperationException(Translations.Get(_currentLanguage.Current, "errors.childUpgrade.notChildRole"));

        await _userManager.RemoveFromRoleAsync(user, "Child");
        await _userManager.AddToRoleAsync(user, "Athlete");

        _auditService.Log("ManualUpgradeToAthlete", "User", user.Id, caller, null);

        var childName = $"{user.FirstName} {user.LastName}";

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            try { await _emailSender.SendChildUpgradeNotificationAsync(user, childName, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to notify child {UserId} of upgrade", user.Id); }
        }

        var parents = await _context.ParentChildren
            .Include(pc => pc.Parent)
            .Where(pc => pc.ChildId == userId)
            .ToListAsync(ct);

        foreach (var pc in parents)
        {
            if (!string.IsNullOrWhiteSpace(pc.Parent.Email))
            {
                try { await _emailSender.SendChildUpgradeNotificationAsync(pc.Parent, childName, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to notify parent {ParentId} of child upgrade", pc.ParentId); }
            }
        }
    }

    public async Task RunPassAsync(CancellationToken ct = default)
    {
        var childRoleId = await _context.Roles
            .Where(r => r.Name == "Child")
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct);

        if (childRoleId == null)
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddYears(-18); // DateOnly comparison is inclusive — a child born exactly on cutoff date is upgraded on their 18th birthday

        var childUserIds = await _context.UserRoles
            .Where(ur => ur.RoleId == childRoleId)
            .Select(ur => ur.UserId)
            .ToListAsync(ct);

        var candidates = await _context.Users
            .Where(u => childUserIds.Contains(u.Id) && u.DateOfBirth != null && u.DateOfBirth <= cutoff)
            .ToListAsync(ct);

        foreach (var user in candidates)
        {
            try
            {
                await _userManager.RemoveFromRoleAsync(user, "Child");
                await _userManager.AddToRoleAsync(user, "Athlete");

                var age = today.Year - user.DateOfBirth!.Value.Year;
                _auditService.Log("ChildAutoUpgraded", "User", user.Id, null, new { age });

                var childName = $"{user.FirstName} {user.LastName}";

                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    try { await _emailSender.SendChildUpgradeNotificationAsync(user, childName, ct); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to notify child {UserId} of upgrade", user.Id); }
                }

                var parents = await _context.ParentChildren
                    .Include(pc => pc.Parent)
                    .Where(pc => pc.ChildId == user.Id)
                    .ToListAsync(ct);

                foreach (var pc in parents)
                {
                    if (!string.IsNullOrWhiteSpace(pc.Parent.Email))
                    {
                        try { await _emailSender.SendChildUpgradeNotificationAsync(pc.Parent, childName, ct); }
                        catch (Exception ex) { _logger.LogWarning(ex, "Failed to notify parent {ParentId} of child upgrade", pc.ParentId); }
                    }
                }

                _logger.LogInformation("Auto-upgraded user {UserId} from Child to Athlete", user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upgrade user {UserId} to Athlete", user.Id);
            }
        }
    }
}

public class ChildUpgradeService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StartupState _startupState;
    private readonly ILogger<ChildUpgradeService> _logger;

    public ChildUpgradeService(
        IServiceScopeFactory scopeFactory,
        StartupState startupState,
        ILogger<ChildUpgradeService> logger)
    {
        _scopeFactory = scopeFactory;
        _startupState = startupState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Same reasoning as TrainingSeriesGeneratorService: the 02:00 schedule makes this safe today
        // by accident, not by design.
        await _startupState.WaitForReadyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            // Target: run once per day at 02:00 UTC. If it is already past 02:00 today, schedule for tomorrow at 02:00.
            var nextRun = now.Date.AddDays(now.Hour >= 2 ? 1 : 0).AddHours(2);
            var delay = nextRun - now;

            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var worker = scope.ServiceProvider.GetRequiredService<ChildUpgradeWorker>();
                await worker.RunPassAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChildUpgradeService pass failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
