using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using ZISK.Client.Services;
using ZISK.Shared.DTOs.Announcements;
using ZISK.Shared.DTOs.Teams;
using ZISK.Shared.Enums;

namespace ZISK.Client.Pages;

public partial class AnnouncementsCreate
{
    [Inject] private IAnnouncementsApi AnnouncementsApi { get; set; } = default!;
    [Inject] private ITeamsApi TeamsApi { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private bool _isSubmitting = false;
    private List<TeamDto> _teams = new();
    private List<IBrowserFile> _selectedFiles = new();

    private AnnouncementCreateFormModel _announcementModel = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _teams = await TeamsApi.GetTeamsAsync(true);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Chyba pri načítaní tímov: {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Error);
        }
    }

    private void Cancel() => NavigationManager.NavigateTo("/oznamy");

    private void OnFilesSelected(InputFileChangeEventArgs args)
    {
        var files = args.GetMultipleFiles(5);
        foreach (var file in files)
        {
            if (file.Size > 10 * 1024 * 1024)
            {
                Snackbar.Add($"Súbor {file.Name} je príliš veľký (max 10MB)", Severity.Warning);
                continue;
            }
            if (!_selectedFiles.Any(f => f.Name == file.Name))
                _selectedFiles.Add(file);
        }
    }

    private void RemoveFile(IBrowserFile file) => _selectedFiles.Remove(file);

    private string GetFileIcon(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => Icons.Material.Filled.PictureAsPdf,
            ".doc" or ".docx" => Icons.Material.Filled.Description,
            ".xls" or ".xlsx" => Icons.Material.Filled.TableChart,
            ".jpg" or ".jpeg" or ".png" or ".gif" => Icons.Material.Filled.Image,
            _ => Icons.Material.Filled.InsertDriveFile
        };
    }

    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private async Task Submit()
    {
        _isSubmitting = true;
        try
        {
            var request = new CreateAnnouncementRequest(
                Title: _announcementModel.Title,
                Content: _announcementModel.Content,
                TargetTeamId: _announcementModel.TargetTeamId,
                TargetAudience: _announcementModel.TargetAudience,
                Priority: _announcementModel.Priority,
                IsPinned: _announcementModel.IsPinned,
                ValidUntil: _announcementModel.ValidUntil
            );

            var announcement = await AnnouncementsApi.CreateAnnouncementAsync(request);

            foreach (var file in _selectedFiles)
            {
                try
                {
                    using var content = new MultipartFormDataContent();
                    using var fileStream = file.OpenReadStream(10 * 1024 * 1024);
                    using var memoryStream = new MemoryStream();
                    await fileStream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var fileContent = new ByteArrayContent(memoryStream.ToArray());
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                        file.ContentType ?? "application/octet-stream");
                    content.Add(fileContent, "file", file.Name);

                    var response = await Http.PostAsync($"/api/announcements/{announcement.Id}/attachments", content);
                    if (!response.IsSuccessStatusCode)
                        Snackbar.Add($"Nepodarilo sa nahrať súbor {file.Name}", Severity.Warning);
                }
                catch (Exception ex)
                {
                    Snackbar.Add($"Chyba pri nahrávaní {file.Name}: {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Warning);
                }
            }

            Snackbar.Add("Oznam bol zverejnený", Severity.Success);
            NavigationManager.NavigateTo("/oznamy");
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Chyba: {ApiErrorFormatter.ToUserMessage(ex)}", Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    public class AnnouncementCreateFormModel
    {
        [Required(ErrorMessage = "Nadpis je povinný.")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "Nadpis musí mať 2 – 200 znakov.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Obsah je povinný.")]
        [StringLength(5000, ErrorMessage = "Obsah môže mať max 5000 znakov.")]
        public string Content { get; set; } = string.Empty;

        public Guid? TargetTeamId { get; set; }
        public TargetAudience TargetAudience { get; set; } = TargetAudience.All;
        public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Medium;
        public bool IsPinned { get; set; } = false;
        public DateTime? ValidUntil { get; set; }
    }
}
