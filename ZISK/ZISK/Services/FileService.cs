using ZISK.Shared.Localization;

namespace ZISK.Services;

public class FileService : IFileService
{
    // Magic-byte signatures for every extension SaveFileAsync callers currently allow. A renamed
    // file (e.g. a script saved as "evil.jpg") passes the extension check but fails this one.
    // .doc/.xls share the OLE2 container signature and .docx/.xlsx share the ZIP signature, since
    // both pairs are literally the same underlying container format.
    private static readonly Dictionary<string, byte[][]> MagicBytesByExtension = new()
    {
        [".pdf"] = [new byte[] { 0x25, 0x50, 0x44, 0x46 }],
        [".png"] = [new byte[] { 0x89, 0x50, 0x4E, 0x47 }],
        [".jpg"] = [new byte[] { 0xFF, 0xD8, 0xFF }],
        [".jpeg"] = [new byte[] { 0xFF, 0xD8, 0xFF }],
        [".gif"] = [new byte[] { 0x47, 0x49, 0x46, 0x38 }],
        [".doc"] = [new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }],
        [".xls"] = [new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }],
        [".docx"] = [new byte[] { 0x50, 0x4B, 0x03, 0x04 }],
        [".xlsx"] = [new byte[] { 0x50, 0x4B, 0x03, 0x04 }],
    };

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

        if (MagicBytesByExtension.TryGetValue(extension, out var signatures) && !HasMatchingSignature(file, signatures))
            return (false, Translations.Get(lang, "errors.file.unsupportedType"));

        return (true, null);
    }

    private static bool HasMatchingSignature(IFormFile file, byte[][] signatures)
    {
        var maxSignatureLength = signatures.Max(s => s.Length);
        var header = new byte[maxSignatureLength];

        using var stream = file.OpenReadStream();
        var bytesRead = stream.Read(header, 0, header.Length);

        return signatures.Any(sig => bytesRead >= sig.Length && header.AsSpan(0, sig.Length).SequenceEqual(sig));
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
