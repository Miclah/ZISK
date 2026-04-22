namespace ZISK.Shared.DTOs.Addresses;

public class NominatimAddressDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Road { get; set; }
    public string? HouseNumber { get; set; }
    public string? Postcode { get; set; }
    public string? City { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string ToFormattedString()
    {
        var streetPart = string.Join(" ", new[] { Road, HouseNumber }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        var cityPart = string.Join(" ", new[] { Postcode, City }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        if (string.IsNullOrWhiteSpace(streetPart) && string.IsNullOrWhiteSpace(cityPart))
            return DisplayName;
        if (string.IsNullOrWhiteSpace(streetPart))
            return cityPart;
        if (string.IsNullOrWhiteSpace(cityPart))
            return streetPart;
        return $"{streetPart}, {cityPart}";
    }
}
