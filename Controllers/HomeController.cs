using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FairwayManager.Models;
using FairwayManager.Services;
using FairwayManager.Data; // ✅ for ApplicationDbContext

namespace FairwayManager.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly GeoLocationService _geoService;
    private readonly ApplicationDbContext _context;

    public HomeController(
        ILogger<HomeController> logger,
        GeoLocationService geoService,
        ApplicationDbContext context)
    {
        _logger = logger;
        _geoService = geoService;
        _context = context;
    }

    public IActionResult Index()
    {
        //  Get user's IP
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        //  Get location
        var location = _geoService.GetLocation(ip ?? "");

        ViewBag.City = location.City;
        ViewBag.State = location.State;
        ViewBag.HasLocation = location.Lat != null;

        // 🧪 Debug
        Console.WriteLine($"IP: {ip}");
        Console.WriteLine($"City: {location.City}, State: {location.State}");

        var tournaments = _context.Tournaments.ToList();

        // If no location → return all tournaments (no filtering)
        if (location.Lat == null || location.Lon == null)
        {
            ViewBag.UpcomingNearby = new List<Tournament>();
            ViewBag.RecentNearby = new List<Tournament>();
            return View(tournaments);
        }

        
        var nearby = tournaments
            .Where(t => t.Latitude != null && t.Longitude != null)
            .Select(t => new
            {
                Tournament = t,
                Distance = CalculateDistance(
                    location.Lat.Value,
                    location.Lon.Value,
                    t.Latitude.Value,
                    t.Longitude.Value)
            })
            .Where(x => x.Distance <= 200)
            .ToList();

        
        var upcoming = nearby
            .Where(x => x.Tournament.Date >= DateTime.UtcNow)
            .OrderBy(x => x.Tournament.Date)
            .Select(x => x.Tournament)
            .ToList();

       
        var recent = nearby
            .Where(x => x.Tournament.Date < DateTime.UtcNow)
            .OrderByDescending(x => x.Tournament.Date)
            .Select(x => x.Tournament)
            .ToList();

        ViewBag.UpcomingNearby = upcoming;
        ViewBag.RecentNearby = recent;

        return View();
    }

    
    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var R = 3958.8; // miles

        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) *
                Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}