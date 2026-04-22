namespace ZISK.Services;

public class NominatimSettings
{
    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";

    public string UserAgent { get; set; } = "ZISK/1.0";

    public string? ContactEmail { get; set; }

    public string BuildUserAgentHeader()
    {
        return string.IsNullOrWhiteSpace(ContactEmail)
            ? UserAgent
            : $"{UserAgent} ({ContactEmail})";
    }
}
