using Microsoft.AspNetCore.Components;
using MudBlazor;
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

    private bool _isLoading = true;
    private bool _dialogVisible = false;
    private List<TeamDto> _teams = new();
    private List<TrainingEventDto> _trainings = new();

    private Guid? _editingId;
    private Guid _teamId;
    private string _title = "";
    private DateTime? _startDate;
    private TimeSpan? _startTime;
    private TimeSpan? _endTime;
    private string _location = "";
    private TrainingType _type = TrainingType.Conditioning;
    private string _coachNote = "";
    private bool _isLocked = false;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _teams = await TeamsApi.GetTeamsAsync(true);
            await LoadTrainings();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Chyba: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadTrainings()
    {
        _trainings = await TrainingsApi.GetTrainingsAsync(null, null, null);
    }

    private void OpenDialog(TrainingEventDto? training)
    {
        if (training != null)
        {
            _editingId = training.Id;
            _teamId = training.TeamId;
            _title = training.Title;
            _startDate = training.StartTime.Date;
            _startTime = training.StartTime.TimeOfDay;
            _endTime = training.EndTime.TimeOfDay;
            _location = training.Location ?? "";
            _type = training.Type;
            _coachNote = training.CoachNote ?? "";
            _isLocked = training.IsLocked;
        }
        else
        {
            _editingId = null;
            _teamId = _teams.FirstOrDefault()?.Id ?? Guid.Empty;
            _title = "";
            _startDate = DateTime.Today;
            _startTime = new TimeSpan(16, 0, 0);
            _endTime = new TimeSpan(17, 30, 0);
            _location = "";
            _type = TrainingType.Conditioning;
            _coachNote = "";
            _isLocked = false;
        }
        _dialogVisible = true;
    }

    private void CloseDialog() => _dialogVisible = false;

    private bool CanSave() =>
        _teamId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(_title) &&
        _startDate.HasValue &&
        _startTime.HasValue &&
        _endTime.HasValue;

    private async Task Save()
    {
        try
        {
            var start = _startDate!.Value.Add(_startTime!.Value);
            var end = _startDate!.Value.Add(_endTime!.Value);

            if (_editingId.HasValue)
            {
                var request = new UpdateTrainingEventRequest(
                    _title, start, end,
                    string.IsNullOrWhiteSpace(_location) ? null : _location,
                    _type,
                    string.IsNullOrWhiteSpace(_coachNote) ? null : _coachNote,
                    _isLocked);
                await TrainingsApi.UpdateTrainingAsync(_editingId.Value, request);
                Snackbar.Add("Tréning upravený", Severity.Success);
            }
            else
            {
                var request = new CreateTrainingEventRequest(
                    _teamId, _title, start, end,
                    string.IsNullOrWhiteSpace(_location) ? null : _location,
                    _type,
                    string.IsNullOrWhiteSpace(_coachNote) ? null : _coachNote);
                await TrainingsApi.CreateTrainingAsync(request);
                Snackbar.Add("Tréning vytvorený", Severity.Success);
            }
            CloseDialog();
            await LoadTrainings();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Chyba: {ex.Message}", Severity.Error);
        }
    }

    private async Task Delete(Guid id)
    {
        try
        {
            await TrainingsApi.DeleteTrainingAsync(id);
            Snackbar.Add("Tréning vymazaný", Severity.Success);
            await LoadTrainings();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Chyba: {ex.Message}", Severity.Error);
        }
    }

    private Color GetTypeColor(TrainingType type) => type switch
    {
        TrainingType.Conditioning => Color.Primary,
        TrainingType.Technical => Color.Secondary,
        TrainingType.Match => Color.Tertiary,
        _ => Color.Default
    };

    private string GetTypeText(TrainingType type) => type switch
    {
        TrainingType.Conditioning => "Kondičný",
        TrainingType.Technical => "Technický",
        TrainingType.Match => "Herný",
        TrainingType.Recovery => "Regeneračný",
        _ => "Iný"
    };
}
