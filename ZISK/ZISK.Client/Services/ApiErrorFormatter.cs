using System.Net;
using System.Text.Json;
using Refit;

namespace ZISK.Client.Services;

public static class ApiErrorFormatter
{
    public static string ToUserMessage(Exception exception, string fallback = "Nastala neočakávaná chyba.")
    {
        if (exception is HttpRequestException)
            return "Nepodarilo sa spojiť so serverom. Skontrolujte pripojenie a skúste znova.";

        if (exception is TaskCanceledException)
            return "Požiadavka bola prerušená. Skúste znova.";

        if (exception is ApiException apiException)
        {
            var contentMessage = ParseApiContent(apiException.Content);
            if (!string.IsNullOrWhiteSpace(contentMessage))
                return contentMessage;

            return apiException.StatusCode switch
            {
                HttpStatusCode.BadRequest => "Neplatné údaje v požiadavke.",
                HttpStatusCode.Unauthorized => "Nie ste prihlásený. Prihláste sa a skúste znova.",
                HttpStatusCode.Forbidden => "Nemáte oprávnenie na túto akciu.",
                HttpStatusCode.NotFound => "Záznam nebol nájdený.",
                HttpStatusCode.InternalServerError => "Nastala chyba na serveri. Skúste to znova.",
                _ => fallback
            };
        }

        return string.IsNullOrWhiteSpace(exception.Message)
            ? fallback
            : exception.Message;
    }

    private static string? ParseApiContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            using var json = JsonDocument.Parse(content);
            var root = json.RootElement;

            if (root.ValueKind == JsonValueKind.String)
                return root.GetString();

            if (root.ValueKind != JsonValueKind.Object)
                return content;

            if (root.TryGetProperty("message", out var messageProperty) && messageProperty.ValueKind == JsonValueKind.String)
                return messageProperty.GetString();

            if (root.TryGetProperty("errors", out var errorsProperty) && errorsProperty.ValueKind == JsonValueKind.Object)
            {
                var parts = new List<string>();
                foreach (var errorEntry in errorsProperty.EnumerateObject())
                {
                    if (errorEntry.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var err in errorEntry.Value.EnumerateArray())
                        {
                            if (err.ValueKind == JsonValueKind.String)
                            {
                                var msg = err.GetString();
                                if (!string.IsNullOrWhiteSpace(msg))
                                    parts.Add(msg);
                            }
                        }
                    }
                }
                if (parts.Count > 0)
                    return string.Join(" | ", parts);
            }

            if (root.TryGetProperty("title", out var titleProperty) && titleProperty.ValueKind == JsonValueKind.String)
                return titleProperty.GetString();
        }
        catch
        {
            return content.Trim('"');
        }

        return content.Trim('"');
    }
}
