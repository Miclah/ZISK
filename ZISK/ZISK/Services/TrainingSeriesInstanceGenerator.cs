using ZISK.Data.Entities;

namespace ZISK.Services;

/// <summary>
/// Shared weekday-bitmask expansion for a <see cref="TrainingSeries"/>.
///
/// Two callers expand a series into concrete trainings: the manual "Generate" action
/// (<see cref="TrainingSeriesService.GenerateInstancesAsync"/>) and the nightly background pass
/// (<see cref="TrainingSeriesGeneratorWorker.GenerateForSeriesAsync"/>). They share this function
/// rather than each writing the loop, so rules like clamping the requested range to the season's
/// date span cannot apply on one path and not the other.
///
/// Deliberately a pure function. It neither reads nor writes the DbContext, so callers stay in
/// charge of loading existing dates and persisting the result.
/// </summary>
public static class TrainingSeriesInstanceGenerator
{
    /// <summary>
    /// Returns the <see cref="TrainingEvent"/> instances that should be created for <paramref name="series"/>
    /// between <paramref name="from"/> and <paramref name="to"/> (both inclusive), skipping any date already
    /// present in <paramref name="existingDates"/>.
    /// </summary>
    /// <param name="series">The series to expand. <see cref="TrainingSeries.Season"/> must be loaded.</param>
    /// <param name="existingDates">
    /// Dates that already have an instance for this series. Mutated as instances are built so a date is never
    /// emitted twice.
    /// </param>
    public static List<TrainingEvent> BuildMissingInstances(
        TrainingSeries series,
        DateOnly from,
        DateOnly to,
        HashSet<DateOnly> existingDates)
    {
        var instances = new List<TrainingEvent>();

        // Clamp the requested range to season boundaries — instances must not fall outside the season.
        // Callers may legitimately pass a wider window (e.g. today + 14 days) that overruns the season end.
        var effectiveFrom = from < series.Season.StartDate ? series.Season.StartDate : from;
        var effectiveTo = to > series.Season.EndDate ? series.Season.EndDate : to;

        if (effectiveFrom > effectiveTo)
            return instances;

        var weekdays = (Weekdays)series.DaysOfWeek; // stored as an int bitmask; cast restores the [Flags] enum
        var createdAt = DateTime.UtcNow;

        for (var date = effectiveFrom; date <= effectiveTo; date = date.AddDays(1))
        {
            // bitwise AND; None (0) means this weekday is not part of the stored mask
            if ((weekdays & ToFlag(date.DayOfWeek)) == Weekdays.None || !existingDates.Add(date))
                continue;

            instances.Add(new TrainingEvent
            {
                Id = Guid.NewGuid(),
                TeamId = series.TeamId,
                SeriesId = series.Id,
                SeasonId = series.SeasonId,
                Title = series.Title,
                StartTime = date.ToDateTime(series.StartTime),
                EndTime = date.ToDateTime(series.EndTime),
                Location = series.Location,
                Type = series.Type,
                CoachNote = series.CoachNote,
                CreatedAt = createdAt
            });
        }

        return instances;
    }

    private static Weekdays ToFlag(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Weekdays.Monday,
        DayOfWeek.Tuesday => Weekdays.Tuesday,
        DayOfWeek.Wednesday => Weekdays.Wednesday,
        DayOfWeek.Thursday => Weekdays.Thursday,
        DayOfWeek.Friday => Weekdays.Friday,
        DayOfWeek.Saturday => Weekdays.Saturday,
        DayOfWeek.Sunday => Weekdays.Sunday,
        _ => Weekdays.None
    };
}
