using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using ZISK.Shared.DTOs.Addresses;

namespace ZISK.Services;

public class NominatimService
{
    public const string HttpClientName = "nominatim";
    private const int RateLimitIntervalMs = 1000;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NominatimService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _nextAllowedRequestUtc = DateTime.MinValue;

    public NominatimService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<NominatimService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public virtual async Task<IReadOnlyList<NominatimAddressDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3)
            return Array.Empty<NominatimAddressDto>();

        var normalized = query.Trim().ToLowerInvariant();
        var cacheKey = $"nominatim:{normalized}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<NominatimAddressDto>? cached) && cached is not null)
            return cached;

        await _gate.WaitAsync(ct);
        try
        {
            var delay = _nextAllowedRequestUtc - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var url = $"search?format=jsonv2&addressdetails=1&limit=6&countrycodes=sk&q={Uri.EscapeDataString(query)}";

            try
            {
                var raw = await client.GetFromJsonAsync<List<NominatimRawResult>>(url, ct)
                          ?? new List<NominatimRawResult>();
                var results = raw.Select(ParseRaw).ToList();
                _cache.Set(cacheKey, (IReadOnlyList<NominatimAddressDto>)results, CacheTtl);
                return results;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "Nominatim lookup failed for query '{Query}'", query);
                return Array.Empty<NominatimAddressDto>();
            }
            finally
            {
                _nextAllowedRequestUtc = DateTime.UtcNow.AddMilliseconds(RateLimitIntervalMs);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public static NominatimAddressDto ParseRaw(NominatimRawResult raw)
    {
        double.TryParse(raw.Lat, System.Globalization.CultureInfo.InvariantCulture, out var lat);
        double.TryParse(raw.Lon, System.Globalization.CultureInfo.InvariantCulture, out var lon);

        var city = raw.Address?.City
                   ?? raw.Address?.Town
                   ?? raw.Address?.Village
                   ?? raw.Address?.Municipality
                   ?? raw.Address?.Suburb;

        return new NominatimAddressDto
        {
            DisplayName = raw.DisplayName ?? string.Empty,
            Road = raw.Address?.Road,
            HouseNumber = raw.Address?.HouseNumber,
            Postcode = raw.Address?.Postcode,
            City = city,
            Latitude = lat,
            Longitude = lon
        };
    }

    public class NominatimRawResult
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }

        [JsonPropertyName("address")]
        public NominatimRawAddress? Address { get; set; }
    }

    public class NominatimRawAddress
    {
        [JsonPropertyName("road")]
        public string? Road { get; set; }

        [JsonPropertyName("house_number")]
        public string? HouseNumber { get; set; }

        [JsonPropertyName("postcode")]
        public string? Postcode { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("town")]
        public string? Town { get; set; }

        [JsonPropertyName("village")]
        public string? Village { get; set; }

        [JsonPropertyName("municipality")]
        public string? Municipality { get; set; }

        [JsonPropertyName("suburb")]
        public string? Suburb { get; set; }
    }
}
