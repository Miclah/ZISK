using Microsoft.EntityFrameworkCore;
using ZISK.Data;

namespace ZISK.Services;

public class AttendanceAutoCloseWorker
{
    private readonly ApplicationDbContext _context;
    private readonly IAttendanceService _attendanceService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AttendanceAutoCloseWorker> _logger;

    public AttendanceAutoCloseWorker(
        ApplicationDbContext context,
        IAttendanceService attendanceService,
        IAuditService auditService,
        ILogger<AttendanceAutoCloseWorker> logger)
    {
        _context = context;
        _attendanceService = attendanceService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task RunPassAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // 30-day lookback window: trainings older than 30 days are assumed to have been handled already
        // and are intentionally excluded to keep each worker pass fast and avoid re-processing old data.
        var cutoff = now.AddDays(-30);

        var pastTrainings = await _context.TrainingEvents
            .Where(te => te.EndTime < now && !te.IsLocked && !te.IsCancelled && te.EndTime > cutoff)
            .ToListAsync(ct);

        foreach (var training in pastTrainings)
        {
            try
            {
                var beforeCount = await _context.AttendanceRecords
                    .CountAsync(ar => ar.TrainingEventId == training.Id, ct);

                await _attendanceService.AutoCompleteForTrainingAsync(training.Id, setLocked: true);

                var afterCount = await _context.AttendanceRecords
                    .CountAsync(ar => ar.TrainingEventId == training.Id, ct);

                _auditService.Log("AttendanceAutoClosed", "TrainingEvent", training.Id.ToString(), null,
                    new { createdCount = afterCount - beforeCount, existingCount = beforeCount });

                _logger.LogInformation("Auto-closed attendance for training {TrainingId} (+{Created} records)", training.Id, afterCount - beforeCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-close attendance for training {TrainingId}", training.Id);
            }
        }
    }
}

public class AttendanceAutoCloseService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StartupState _startupState;
    private readonly ILogger<AttendanceAutoCloseService> _logger;

    public AttendanceAutoCloseService(
        IServiceScopeFactory scopeFactory,
        StartupState startupState,
        ILogger<AttendanceAutoCloseService> logger)
    {
        _scopeFactory = scopeFactory;
        _startupState = startupState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // This worker queries in its very first iteration, and migrations no longer finish before
        // the hosted services start. Without this it would run against a schema-less database.
        await _startupState.WaitForReadyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var worker = scope.ServiceProvider.GetRequiredService<AttendanceAutoCloseWorker>();
                await worker.RunPassAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AttendanceAutoCloseService pass failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
