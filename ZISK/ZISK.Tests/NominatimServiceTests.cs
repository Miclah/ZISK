using System.Text.Json;
using ZISK.Services;

namespace ZISK.Tests;

public class NominatimServiceTests
{
    [Fact]
    public void ParseRaw_BuildsFormattedAddress_FromFullResponse()
    {
        var json = """
        {
            "display_name": "Hlavná 42, 010 01 Žilina, Slovensko",
            "lat": "49.223456",
            "lon": "18.740123",
            "address": {
                "road": "Hlavná",
                "house_number": "42",
                "postcode": "010 01",
                "city": "Žilina"
            }
        }
        """;

        var raw = JsonSerializer.Deserialize<NominatimService.NominatimRawResult>(json)!;
        var dto = NominatimService.ParseRaw(raw);

        Assert.Equal("Hlavná", dto.Road);
        Assert.Equal("42", dto.HouseNumber);
        Assert.Equal("010 01", dto.Postcode);
        Assert.Equal("Žilina", dto.City);
        Assert.Equal(49.223456, dto.Latitude, 5);
        Assert.Equal(18.740123, dto.Longitude, 5);
        Assert.Equal("Hlavná 42, 010 01 Žilina", dto.ToFormattedString());
    }

    [Fact]
    public void ParseRaw_FallsBackToTown_WhenCityMissing()
    {
        var json = """
        {
            "display_name": "Krížna 10, 987 01 Poltár",
            "lat": "48.43",
            "lon": "19.79",
            "address": {
                "road": "Krížna",
                "house_number": "10",
                "postcode": "987 01",
                "town": "Poltár"
            }
        }
        """;

        var raw = JsonSerializer.Deserialize<NominatimService.NominatimRawResult>(json)!;
        var dto = NominatimService.ParseRaw(raw);

        Assert.Equal("Poltár", dto.City);
    }

    [Fact]
    public void ParseRaw_FallsBackToVillage_WhenCityAndTownMissing()
    {
        var json = """
        {
            "display_name": "Slnečná 5, 962 31 Sliač",
            "lat": "48.6",
            "lon": "19.15",
            "address": {
                "road": "Slnečná",
                "house_number": "5",
                "postcode": "962 31",
                "village": "Sliač"
            }
        }
        """;

        var raw = JsonSerializer.Deserialize<NominatimService.NominatimRawResult>(json)!;
        var dto = NominatimService.ParseRaw(raw);

        Assert.Equal("Sliač", dto.City);
    }

    [Fact]
    public void ParseRaw_FallsBackToMunicipality_AndThenSuburb()
    {
        var municipalityJson = """
        {
            "display_name": "X",
            "lat": "0",
            "lon": "0",
            "address": { "municipality": "Obec Stráne" }
        }
        """;
        var suburbJson = """
        {
            "display_name": "Y",
            "lat": "0",
            "lon": "0",
            "address": { "suburb": "Karlova Ves" }
        }
        """;

        var munDto = NominatimService.ParseRaw(
            JsonSerializer.Deserialize<NominatimService.NominatimRawResult>(municipalityJson)!);
        var subDto = NominatimService.ParseRaw(
            JsonSerializer.Deserialize<NominatimService.NominatimRawResult>(suburbJson)!);

        Assert.Equal("Obec Stráne", munDto.City);
        Assert.Equal("Karlova Ves", subDto.City);
    }

    [Fact]
    public void ParseRaw_UsesInvariantCulture_ForCoordinateParsing()
    {
        var json = """
        {
            "display_name": "Test",
            "lat": "48.123456",
            "lon": "17.987654",
            "address": {}
        }
        """;

        var raw = JsonSerializer.Deserialize<NominatimService.NominatimRawResult>(json)!;
        var dto = NominatimService.ParseRaw(raw);

        Assert.Equal(48.123456, dto.Latitude, 6);
        Assert.Equal(17.987654, dto.Longitude, 6);
    }

    [Fact]
    public void ParseRaw_SetsZeroCoordinates_WhenLatLonUnparseable()
    {
        var json = """
        { "display_name": "Test", "lat": "nope", "lon": "also-nope", "address": {} }
        """;

        var raw = JsonSerializer.Deserialize<NominatimService.NominatimRawResult>(json)!;
        var dto = NominatimService.ParseRaw(raw);

        Assert.Equal(0d, dto.Latitude);
        Assert.Equal(0d, dto.Longitude);
    }

    [Fact]
    public void ToFormattedString_ReturnsDisplayName_WhenStreetAndCityMissing()
    {
        var raw = new NominatimService.NominatimRawResult
        {
            DisplayName = "Len display name",
            Lat = "0",
            Lon = "0",
            Address = new NominatimService.NominatimRawAddress()
        };

        var dto = NominatimService.ParseRaw(raw);

        Assert.Equal("Len display name", dto.ToFormattedString());
    }
}
