using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Seasons;

namespace ZISK.Services;

public class SeasonService : ISeasonService
{
    private readonly ApplicationDbContext _context;

    public SeasonService(ApplicationDbContext context)
    {
        _context = context;
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
            throw new InvalidOperationException("Aktívnu sezónu nemožno vymazať.");

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

    private static void ValidateDates(DateOnly start, DateOnly end)
    {
        if (start >= end)
            throw new ArgumentException("Dátum začiatku musí byť pred dátumom konca.");
    }

    private static SeasonDto ToDto(Season s) =>
        new(s.Id, s.Name, s.StartDate, s.EndDate, s.IsActive, s.CreatedAt);
}
