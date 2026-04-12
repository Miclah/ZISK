using System.ComponentModel.DataAnnotations;
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
    private bool _isLocked = false;
    private TrainingFormModel _trainingModel = new();

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
            _isLocked = training.IsLocked;
            _trainingModel = new TrainingFormModel
            {
                TeamId = training.TeamId,
                Title = training.Title,
                StartDate = training.StartTime.Date,
                StartTime = training.StartTime.TimeOfDay,
                EndTime = training.EndTime.TimeOfDay,
                Location = string.IsNullOrWhiteSpace(training.Location) ? null : training.Location,
                Type = training.Type,
                CoachNote = string.IsNullOrWhiteSpace(training.CoachNote) ? null : training.CoachNote
            };
        }
        else
        {
            _editingId = null;
            _isLocked = false;
            _trainingModel = new TrainingFormModel
            {
                TeamId = _teams.FirstOrDefault()?.Id,
                StartDate = DateTime.Today,
                StartTime = new TimeSpan(16, 0, 0),
                EndTime = new TimeSpan(17, 30, 0),
                Type = TrainingType.Conditioning
            };
        }
        _dialogVisible = true;
    }

    private void CloseDialog() => _dialogVisible = false;

    private async Task Save()
    {
        try
        {
            var start = _trainingModel.StartDate!.Value.Add(_trainingModel.StartTime!.Value);
            var end = _trainingModel.StartDate!.Value.Add(_trainingModel.EndTime!.Value);

            if (_editingId.HasValue)
            {
                var request = new UpdateTrainingEventRequest(
                    _trainingModel.Title,
                    start,
                    end,
                    string.IsNullOrWhiteSpace(_trainingModel.Location) ? null : _trainingModel.Location,
                    _trainingModel.Type,
                    string.IsNullOrWhiteSpace(_trainingModel.CoachNote) ? null : _trainingModel.CoachNote,
                    _isLocked);
                await TrainingsApi.UpdateTrainingAsync(_editingId.Value, request);
                Snackbar.Add("Tréning upravený", Severity.Success);
            }
            else
            {
                var request = new CreateTrainingEventRequest(
                    _trainingModel.TeamId!.Value,
                    _trainingModel.Title,
                    start,
                    end,
                    string.IsNullOrWhiteSpace(_trainingModel.Location) ? null : _trainingModel.Location,
                    _trainingModel.Type,
                    string.IsNullOrWhiteSpace(_trainingModel.CoachNote) ? null : _trainingModel.CoachNote);
                await TrainingsApi.CreateTrainingAsync(request);
                Snackbar.Add("Tréning vytvorený", Severity.Success);
            }
            CloseDialog();
            await LoadTrainings();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Chyba: {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Error);
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
            Snackbar.Add($"Chyba: {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Error);
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

    public class TrainingFormModel
    {
        [Required(ErrorMessage = "Tím je povinný.")]
        public Guid? TeamId { get; set; }

        [Required(ErrorMessage = "Názov je povinný.")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "Názov musí mať 2 – 200 znakov.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Dátum je povinný.")]
        public DateTime? StartDate { get; set; }

        [Required(ErrorMessage = "Čas začiatku je povinný.")]
        public TimeSpan? StartTime { get; set; }

        [Required(ErrorMessage = "Čas konca je povinný.")]
        public TimeSpan? EndTime { get; set; }

        [StringLength(200, ErrorMessage = "Miesto môže mať max 200 znakov.")]
        public string? Location { get; set; }

        public TrainingType Type { get; set; } = TrainingType.Conditioning;

        [StringLength(1000, ErrorMessage = "Poznámka môže mať max 1000 znakov.")]
        public string? CoachNote { get; set; }
    }
}
