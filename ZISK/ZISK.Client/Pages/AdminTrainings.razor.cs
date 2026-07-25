using Microsoft.AspNetCore.Components;
using MudBlazor;
using ZISK.Client.Components;
using ZISK.Client.Layout;
using ZISK.Client.Services;
using ZISK.Shared.DTOs.Teams;
using ZISK.Shared.DTOs.Trainings;
using ZISK.Shared.Enums;

namespace ZISK.Client.Pages;

public partial class AdminTrainings
{
    [Inject] private ITeamsApi TeamsApi { get; set; } = default!;
    [Inject] private ITrainingsApi TrainingsApi { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private bool _isLoading = true;
    private int _activeTab;
    private List<TeamDto> _teams = new();
    private List<TrainingEventDto> _trainings = new();

    private Guid? _filterTeamId;
    private TrainingType? _filterType;
    // Manual mobile pagination instead of MudTable's built-in pager
    // MudTable has performance issues with long lists on small screens.
    private const int MobilePageSize = 10;
    private int _mobileShown = MobilePageSize;

    private IEnumerable<TrainingEventDto> FilteredTrainings =>
        _trainings.Where(t =>
            (_filterTeamId is null || t.TeamId == _filterTeamId) &&
            (_filterType is null || t.Type == _filterType));

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _teams = await TeamsApi.GetTeamsAsync(true);
            await LoadTrainings();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Chyba: {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }

        // Deep-link support: other pages navigate here with ?action=create to open the create dialog automatically.
        if (NavigationManager.Uri.Contains("action=create", StringComparison.OrdinalIgnoreCase))
            await OpenCreateDialog();
    }

    private async Task LoadTrainings()
    {
        _trainings = await TrainingsApi.GetTrainingsAsync(null, null, null);
        _mobileShown = MobilePageSize;
    }

    private void OnFilterChanged()
    {
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
        var dialog = await DialogService.ShowAsync<TrainingFormDialog>("Nový tréning", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            Snackbar.Add("Tréning vytvorený", Severity.Success);
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
        var dialog = await DialogService.ShowAsync<TrainingFormDialog>("Upraviť tréning", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            Snackbar.Add("Tréning upravený", Severity.Success);
            await LoadTrainings();
        }
    }

    private async Task ConfirmDeleteAsync(TrainingEventDto training)
    {
        bool? confirm = await DialogService.ShowMessageBoxAsync(
            "Vymazať tréning",
            $"Naozaj chcete vymazať tréning '{training.Title}' ({training.StartTime:dd.MM.yyyy HH:mm})?",
            yesText: "Áno, vymazať",
            cancelText: "Zrušiť",
            options: new DialogOptions { MaxWidth = MaxWidth.Small });

        if (confirm != true) return;

        try
        {
            await TrainingsApi.DeleteTrainingAsync(training.Id);
            Snackbar.Add("Tréning vymazaný", Severity.Success);
            await LoadTrainings();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Chyba: {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Error);
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

    private static string GetTypeText(TrainingType type) => type switch
    {
        TrainingType.Conditioning => "Kondičný",
        TrainingType.Technical => "Technický",
        TrainingType.Match => "Herný",
        TrainingType.Recovery => "Regeneračný",
        _ => "Iný"
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
