namespace ZISK.Services;

public interface IFileService
{
    (bool IsValid, string? ErrorMessage) ValidateFile(IFormFile file, long maxSizeBytes, string[] allowedExtensions);
    Task<string> SaveFileAsync(IFormFile file, string subfolder);
    void DeleteFile(string relativePath);
    string GetContentType(string filePath);
}
