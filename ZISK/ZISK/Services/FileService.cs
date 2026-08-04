using ZISK.Shared.Localization;

namespace ZISK.Services;

public class FileService : IFileService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ICurrentLanguage _currentLanguage;

    public FileService(IWebHostEnvironment environment, ICurrentLanguage currentLanguage)
    {
        _environment = environment;
        _currentLanguage = currentLanguage;
    }

    public (bool IsValid, string? ErrorMessage) ValidateFile(IFormFile file, long maxSizeBytes, string[] allowedExtensions)
    {
        var lang = _currentLanguage.Current;

        if (file == null || file.Length == 0)
            return (false, Translations.Get(lang, "errors.file.empty"));

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return (false, Translations.Get(lang, "errors.file.unsupportedType"));

        if (file.Length > maxSizeBytes)
            return (false, string.Format(Translations.Get(lang, "errors.file.tooLarge"), maxSizeBytes / 1024 / 1024));

        return (true, null);
    }

    public async Task<string> SaveFileAsync(IFormFile file, string subfolder)
    {
        var uploadsFolder = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", subfolder);
        Directory.CreateDirectory(uploadsFolder);

        // Guid prefix makes the filename globally unique — prevents collisions when multiple users upload files with the same name.
        // Path.GetExtension includes the leading dot (e.g. ".pdf"), so no separator is needed before it.
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/{subfolder}/{uniqueFileName}";
    }

    public void DeleteFile(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return;

        var fullPath = Path.Combine(_environment.WebRootPath ?? "wwwroot", relativePath.TrimStart('/'));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }
}
