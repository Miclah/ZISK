using Microsoft.AspNetCore.Components;
using MudBlazor;
using ZISK.Client.Components;
using ZISK.Client.Layout;
using ZISK.Client.Services;
using ZISK.Shared.DTOs.Seasons;
using ZISK.Shared.DTOs.Teams;
using ZISK.Shared.DTOs.Trainings;
using ZISK.Shared.Enums;

namespace ZISK.Client.Pages;

public partial class AdminTrainings
{
    [Inject] private ITeamsApi TeamsApi { get; set; } = default!;
    [Inject] private ITrainingsApi TrainingsApi { get; set; } = default!;
    [Inject] private ISeasonsApi SeasonsApi { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private bool _isLoading = true;
    private int _activeTab;
    private List<TeamDto> _teams = new();
    private List<TrainingEventDto> _trainings = new();

    private Guid? _filterTeamId;
    private TrainingType? _filterType;

    // The trainings endpoint returns everything ever recorded when no date range is given, and that set only
    // grows season after season. The page now fetches one season at a time, defaulting to the active one;
    // picking "Všetky sezóny" is an explicit opt-in to the unbounded fetch.
    private List<SeasonDto> _seasons = new();
    private Guid? _filterSeasonId;
    // Manual mobile pagination instead of MudTable's built-in pager
    // MudTable has performance issues with long lists on small screens.
    private const int MobilePageSize = 10;
    private int _mobileShown = MobilePageSize;

    // Materialized once per filter change rather than recomputed per render: the markup touches this in four
    // places (count label, desktop table, mobile slice, mobile total), so as a lazy IEnumerable the whole
    // filter ran four times on every render.
    private List<TrainingEventDto> _filteredTrainings = new();

    private void ApplyFilter()
    {
        _filteredTrainings = _trainings
            .Where(t =>
                (_filterTeamId is null || t.TeamId == _filterTeamId) &&
                (_filterType is null || t.Type == _filterType))
            .ToList();
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var teamsTask = TeamsApi.GetTeamsAsync(true);
            var seasonsTask = SeasonsApi.GetSeasonsAsync();
            await Task.WhenAll(teamsTask, seasonsTask);

            _teams = await teamsTask;
            _seasons = (await seasonsTask).OrderByDescending(s => s.StartDate).ToList();

            // Default to the active season; fall back to the most recent one, or to unbounded if none exist.
            _filterSeasonId = (_seasons.FirstOrDefault(s => s.IsActive) ?? _seasons.FirstOrDefault())?.Id;

            await LoadTrainings();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"{T("attendance.errorGeneric")} {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }

        // Deep-link support: other pages navigate here with ?action=create to open the create dialog automatically.
        if (CreateDeepLink.Consume(NavigationManager))
            await OpenCreateDialog();
    }

    private async Task LoadTrainings()
    {
        var season = _seasons.FirstOrDefault(s => s.Id == _filterSeasonId);

        // Season bounds are dates; the endpoint takes DateTimes, so the end is pushed to the end of that day.
        DateTime? from = season is null ? null : season.StartDate.ToDateTime(TimeOnly.MinValue);
        DateTime? to = season is null ? null : season.EndDate.ToDateTime(TimeOnly.MaxValue);

        _trainings = await TrainingsApi.GetTrainingsAsync(null, from, to);
        ApplyFilter();
        _mobileShown = MobilePageSize;
    }

    private async Task OnSeasonChanged(Guid? seasonId)
    {
        _filterSeasonId = seasonId;
        _isLoading = true;
        try
        {
            await LoadTrainings();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"{T("attendance.errorGeneric")} {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OnFilterChanged()
    {
        ApplyFilter();
        _mobileShown = MobilePageSize;
    }

    private void LoadMoreMobile()
    {
        _mobileShown += MobilePageSize;
    }

    private async Task OpenCreateDialog()
    {
        var parameters = new DialogParameters
        {
            [nameof(TrainingFormDialog.Teams)] = _teams,
            [nameof(TrainingFormDialog.DefaultTeamId)] = (Guid?)_teams.FirstOrDefault()?.Id
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true };
        var dialog = await DialogService.ShowAsync<TrainingFormDialog>(T("admin.trainings.newTraining"), parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            Snackbar.Add(T("admin.trainings.trainingCreated"), Severity.Success);
            await LoadTrainings();
        }
    }

    private async Task OpenEditDialog(TrainingEventDto training)
    {
        var parameters = new DialogParameters
        {
            [nameof(TrainingFormDialog.Teams)] = _teams,
            [nameof(TrainingFormDialog.Existing)] = training
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true };
        var dialog = await DialogService.ShowAsync<TrainingFormDialog>(T("admin.trainings.editTraining"), parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            Snackbar.Add(T("admin.trainings.trainingUpdated"), Severity.Success);
            await LoadTrainings();
        }
    }

    private async Task ConfirmDeleteAsync(TrainingEventDto training)
    {
        bool? confirm = await DialogService.ShowMessageBoxAsync(
            T("admin.trainings.deleteTrainingTitle"),
            string.Format(T("admin.trainings.deleteConfirmText"), training.Title, training.StartTime.ToString("dd.MM.yyyy HH:mm")),
            yesText: T("admin.users.yesDelete"),
            cancelText: T("admin.users.cancel"),
            options: new DialogOptions { MaxWidth = MaxWidth.Small });

        if (confirm != true) return;

        try
        {
            await TrainingsApi.DeleteTrainingAsync(training.Id);
            Snackbar.Add(T("admin.trainings.trainingDeleted"), Severity.Success);
            await LoadTrainings();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"{T("attendance.errorGeneric")} {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Error);
        }
    }

    private static Color GetTypeColor(TrainingType type) => type switch
    {
        TrainingType.Conditioning => Color.Primary,
        TrainingType.Technical => Color.Secondary,
        TrainingType.Match => Color.Tertiary,
        TrainingType.Recovery => Color.Info,
        _ => Color.Default
    };

    // Reads colors directly from AppTheme so border colors stay in sync with the MudBlazor theme.
    // Hardcoded HEX strings here would go out of sync whenever the theme palette changes.
    private static string GetTypeBorderColor(TrainingType type)
    {
        var palette = AppTheme.SportClubTheme.PaletteLight;
        return type switch
        {
            TrainingType.Conditioning => palette.Primary.Value,
            TrainingType.Technical => palette.Secondary.Value,
            TrainingType.Match => palette.Tertiary.Value,
            TrainingType.Recovery => palette.Info.Value,
            _ => palette.ActionDefault.Value
        };
    }
}
