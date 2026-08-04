using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Seasons;
using ZISK.Shared.Localization;

namespace ZISK.Services;

public class SeasonService : ISeasonService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentLanguage _currentLanguage;

    public SeasonService(ApplicationDbContext context, ICurrentLanguage currentLanguage)
    {
        _context = context;
        _currentLanguage = currentLanguage;
    }

    public async Task<List<SeasonDto>> GetSeasonsAsync()
    {
        return await _context.Seasons
            .AsNoTracking()
            .OrderByDescending(s => s.StartDate)
            .Select(s => ToDto(s))
            .ToListAsync();
    }

    public async Task<SeasonDto> GetSeasonAsync(Guid id)
    {
        var season = await _context.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException();
        return ToDto(season);
    }

    public async Task<SeasonDto> CreateSeasonAsync(CreateSeasonRequest request)
    {
        ValidateDates(request.StartDate, request.EndDate);

        var season = new Season
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return ToDto(season);
    }

    public async Task<SeasonDto> UpdateSeasonAsync(Guid id, UpdateSeasonRequest request)
    {
        ValidateDates(request.StartDate, request.EndDate);

        var season = await _context.Seasons.FindAsync(id) ?? throw new KeyNotFoundException();
        season.Name = request.Name;
        season.StartDate = request.StartDate;
        season.EndDate = request.EndDate;

        await _context.SaveChangesAsync();
        return ToDto(season);
    }

    public async Task DeleteSeasonAsync(Guid id)
    {
        var season = await _context.Seasons.FindAsync(id) ?? throw new KeyNotFoundException();
        if (season.IsActive)
            throw new InvalidOperationException(Translations.Get(_currentLanguage.Current, "errors.season.cannotDeleteActive"));

        // TrainingEvent.SeasonId and TrainingSeries.SeasonId are Restrict FKs - without this
        // check, SaveChangesAsync would fail with a raw FK violation instead of a clear message.
        var hasTrainings = await _context.TrainingEvents.AnyAsync(te => te.SeasonId == id);
        var hasSeries = await _context.TrainingSeries.AnyAsync(ts => ts.SeasonId == id);
        if (hasTrainings || hasSeries)
            throw new InvalidOperationException(Translations.Get(_currentLanguage.Current, "errors.season.cannotDeleteHasTrainings"));

        _context.Seasons.Remove(season);
        await _context.SaveChangesAsync();
    }

    public async Task<SeasonDto> ActivateAsync(Guid id)
    {
        // Serializable is the strictest isolation level — it prevents a race condition where two concurrent
        // activation requests could both pass the "deactivate all" step and leave two active seasons at once.
        await using var transaction = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        var active = await _context.Seasons.Where(s => s.IsActive).ToListAsync();
        foreach (var s in active)
            s.IsActive = false;

        var season = await _context.Seasons.FindAsync(id) ?? throw new KeyNotFoundException();
        season.IsActive = true;

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ToDto(season);
    }

    private void ValidateDates(DateOnly start, DateOnly end)
    {
        if (start >= end)
            throw new ArgumentException(Translations.Get(_currentLanguage.Current, "errors.season.startBeforeEnd"));
    }

    private static SeasonDto ToDto(Season s) =>
        new(s.Id, s.Name, s.StartDate, s.EndDate, s.IsActive, s.CreatedAt);
}
