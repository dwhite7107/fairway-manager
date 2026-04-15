using System.IO;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;

public class GeoLocationService
{
    private readonly DatabaseReader _reader;

    public GeoLocationService()
    {
        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "GeoLite2-City.mmdb");

        if (!File.Exists(dbPath))
        {
            throw new Exception($"GeoLite2 database not found at: {dbPath}");
        }

        _reader = new DatabaseReader(dbPath);
    }

    public (string? City, string? State, double? Lat, double? Lon) GetLocation(string ip)
    {
        try
        {
            if (ip == "127.0.0.1" || ip == "::1")
            {
                // Default for local testing
                return ("Oxford", "MS", 34.3665, -89.5187);
            }

            var response = _reader.City(ip);

            return (
                response.City.Name,
                response.MostSpecificSubdivision.Name,
                response.Location.Latitude,
                response.Location.Longitude
            );
        }
        catch
        {
            return (null, null, null, null);
        }
    }
}