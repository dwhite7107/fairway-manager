using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FairwayManager.Models;
using FairwayManager.Services;
using FairwayManager.Data;
using System.Security.Claims;

namespace FairwayManager.Controllers
{
    public class TournamentController : Controller
    {
        private readonly TournamentService _tournamentService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public TournamentController(
            TournamentService tournamentService,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context)
        {
            _tournamentService = tournamentService;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var tournaments = await _tournamentService.GetAllTournamentsAsync();

            var today = DateTime.Today;

            foreach (var t in tournaments)
            {
                var endDate = t.Date.AddDays(t.NumberOfRounds - 1);

                if (today < t.Date)
                    t.Status = "Upcoming";
                else if (today <= endDate)
                    t.Status = "In Progress";
                else
                    t.Status = "Completed";
            }

            return View(tournaments);
        }

        // GET: Tournament/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var tournament = await _context.Tournaments
                .Include(t => t.TournamentPlayers)
                    .ThenInclude(tp => tp.Player)
                .Include(t => t.TournamentTeams)
                    .ThenInclude(tt => tt.Team)
                        .ThenInclude(team => team.PlayerTeams)
                            .ThenInclude(pt => pt.Player)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);

            if (user != null)
            {
                ViewBag.CurrentUserId = user.Id;
            }

            return View(tournament);
        }

        // POST: Tournament/Join
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Join(int id, string code)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized();

            var tournament = await _context.Tournaments
                .Include(t => t.TournamentPlayers)
                .ThenInclude(tp => tp.Player)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null)
                return NotFound();

            if (tournament.JoinCode != code)
            {
                ViewBag.ErrorMessage = "Invalid join code.";
                ViewBag.CurrentUserId = user.Id;
                return View("Details", tournament);
            }

            var player = await _context.Players
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (player == null)
            {
                player = new Player
                {
                    UserId = user.Id,
                    Name = user.UserName 
                };

                _context.Players.Add(player);
                await _context.SaveChangesAsync();
            }

            var alreadyJoined = await _context.TournamentPlayers
                .AnyAsync(tp => tp.TournamentId == id && tp.PlayerId == player.Id);

            if (alreadyJoined)
                return RedirectToAction(nameof(Details), new { id });

            var tournamentPlayer = new TournamentPlayer
            {
                TournamentId = id,
                PlayerId = player.Id
            };

            _context.TournamentPlayers.Add(tournamentPlayer);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Tournament/Create
        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Tournament/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Tournament tournament)
        {
            if (ModelState.IsValid)
            {
                // ✅ Fix DateTime for PostgreSQL (UTC required)
                tournament.Date = DateTime.SpecifyKind(tournament.Date, DateTimeKind.Utc);

                
                if (string.IsNullOrEmpty(tournament.ScoringType))
                {
                    tournament.ScoringType = "Stroke Play";
                }

                // ✅ Generate Join Code
                tournament.JoinCode = GenerateJoinCode();

                // 🔥 Geocode if missing (manual entry fallback)
                if (tournament.Latitude == null || tournament.Longitude == null)
                {
                    try
                    {
                        var query = $"{tournament.CourseName} {tournament.City} {tournament.State}";
                        var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=1";

                        using (var client = new HttpClient())
                        {
                            client.DefaultRequestHeaders.UserAgent.ParseAdd("FairwayManagerApp/1.0");

                            var response = await client.GetStringAsync(url);

                            var results = System.Text.Json.JsonSerializer.Deserialize<List<NominatimResult>>(response);

                            if (results != null && results.Count > 0)
                            {
                                tournament.Latitude = double.Parse(results[0].lat);
                                tournament.Longitude = double.Parse(results[0].lon);
                            }
                        }
                    }
                    catch
                    {
                        // Fail silently (do not block tournament creation)
                    }
                }

                tournament.CreatorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                _context.Add(tournament);
                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }

            return View(tournament);
        }

        [AllowAnonymous]
        public IActionResult Browse(string statusFilter, DateTime? startDate, DateTime? endDate, int page = 1)
        {
            int pageSize = 6;

            var today = DateTime.Today;

            var tournaments = _context.Tournaments
                .Include(t => t.TournamentPlayers)
                .ToList();

            // 🔥 APPLY STATUS LOGIC USING NumberOfRounds
            var result = tournaments.Select(t =>
            {
                var endDateCalc = t.Date.AddDays(t.NumberOfRounds - 1);

                string status;

                if (today < t.Date)
                    status = "Upcoming";
                else if (today <= endDateCalc)
                    status = "In Progress";
                else
                    status = "Completed";

                // ✅ attach status back to tournament object
                t.Status = status;

                return t;
            });

            // 🔍 FILTERS
            if (!string.IsNullOrEmpty(statusFilter))
                result = result.Where(t => t.Status == statusFilter);

            if (startDate.HasValue)
                result = result.Where(t => t.Date >= startDate.Value);

            if (endDate.HasValue)
                result = result.Where(t => t.Date <= endDate.Value);

            // 📄 PAGINATION
            int totalItems = result.Count();

            var pagedData = result
                .OrderByDescending(t => t.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)totalItems == 0 ? 1 : totalItems / (double)pageSize);

            return View(pagedData);
        }

        // GET: Manage Players
        public async Task<IActionResult> ManagePlayers(int id)
        {
            var tournament = await _tournamentService.GetTournamentByIdAsync(id);

            if (tournament == null)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);

            if (user == null || tournament.CreatorId != user.Id)
                return Forbid();

            return View(tournament);
        }

        // GET: Set Pars
        public IActionResult SetPars(int id)
        {
            var tournament = _context.Tournaments
                .FirstOrDefault(t => t.Id == id);

            if (tournament == null)
                return NotFound();

            var pars = _context.TournamentHolePars
                .Where(p => p.TournamentId == id)
                .ToList();

            ViewBag.TournamentId = id;
            ViewBag.HoleCount = tournament.HoleCount;
            ViewBag.Pars = pars;

            return View();
        }

        // POST: Set Pars
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPars(int tournamentId, List<int> pars)
        {
            var existingPars = _context.TournamentHolePars
                .Where(p => p.TournamentId == tournamentId)
                .ToList();

            for (int i = 0; i < pars.Count; i++)
            {
                int holeNumber = i + 1;

                var existing = existingPars
                    .FirstOrDefault(p => p.HoleNumber == holeNumber);

                if (existing != null)
                {
                    // UPDATE
                    existing.Par = pars[i];
                }
                else
                {
                    // CREATE
                    _context.TournamentHolePars.Add(new TournamentHolePar
                    {
                        TournamentId = tournamentId,
                        HoleNumber = holeNumber,
                        Par = pars[i]
                    });
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("SetPars", new { id = tournamentId });
        }


        [Authorize]
        public IActionResult EditProfile()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var player = _context.Players.FirstOrDefault(p => p.UserId == userId);

            if (player == null)
                return NotFound();

            return View(player);
        }
        [HttpPost]
        public IActionResult EditProfile(Player updatedPlayer)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var player = _context.Players.FirstOrDefault(p => p.UserId == userId);

            if (player == null)
                return NotFound();

            player.Name = updatedPlayer.Name;
            player.PhoneNumber = updatedPlayer.PhoneNumber;
            player.HomeCourse = updatedPlayer.HomeCourse;

            _context.SaveChanges();

            return RedirectToAction("EditProfile");
        }
        
        [Authorize]
        public IActionResult EnterScores(int id, int round = 1)
        {
            var tournament = _context.Tournaments
                .Include(t => t.TournamentPlayers)
                    .ThenInclude(tp => tp.Player)
                .Include(t => t.TournamentTeams)
                    .ThenInclude(tt => tt.Team)
                .FirstOrDefault(t => t.Id == id);

            if (tournament == null)
                return NotFound();
            
            var endDate = tournament.Date.AddDays(tournament.NumberOfRounds - 1);

            if (DateTime.Today > endDate)
            {
                return RedirectToAction("Details", new { id });
            }

            if (tournament.ScoringType == "Scramble")
            {
                return RedirectToAction("EnterScrambleScores", new { id });
            }

            
            var scores = _context.Scores
                .Where(s => s.TournamentId == id)
                .ToList();
            
            ViewBag.CurrentRound = round;

            ViewBag.AllScores = scores;
            
            ViewBag.TournamentPlayers = tournament.TournamentPlayers;


            ViewBag.Scores = scores;



            return View(tournament);
        }

        [HttpPost]
        [Authorize]
        public IActionResult EnterScores(int id, int round, List<int> strokes, List<int> playerIds, List<int> holeNumbers)
        {

            var tournament = _context.Tournaments.Find(id);

            var endDate = tournament.Date.AddDays(tournament.NumberOfRounds - 1);

            if (DateTime.Today > endDate)
            {
                return RedirectToAction("Details", new { id });
            }
            
            for (int i = 0; i < strokes.Count; i++)
            {
                var existingScore = _context.Scores.FirstOrDefault(s =>
                    s.TournamentId == id &&
                    s.PlayerId == playerIds[i] &&
                    s.HoleNumber == holeNumbers[i] &&
                    s.RoundNumber == round);

                if (existingScore != null)
                {
                    existingScore.Strokes = strokes[i];
                }
                else
                {
                    var score = new Score
                    {
                        TournamentId = id,
                        PlayerId = playerIds[i],
                        HoleNumber = holeNumbers[i],
                        RoundNumber = round, // 🔥 THIS FIXES YOUR ISSUE
                        Strokes = strokes[i]
                    };

                    _context.Scores.Add(score);
                }
            }

            _context.SaveChanges();

            return RedirectToAction("EnterScores", new { id, round });
        }




        public IActionResult EnterScrambleScores(int id, int round = 1)
        {
            var tournament = _context.Tournaments
                .Include(t => t.TournamentTeams)
                    .ThenInclude(tt => tt.Team)
                        .ThenInclude(team => team.PlayerTeams)
                            .ThenInclude(pt => pt.Player)
                .FirstOrDefault(t => t.Id == id);

            if (tournament == null)
                return NotFound();
            
            var endDate = tournament.Date.AddDays(tournament.NumberOfRounds - 1);

            if (DateTime.Today > endDate)
            {
                return RedirectToAction("Details", new { id });
            }

            // ✅ Filter scores by BOTH tournament AND round
            var teamScores = _context.TeamScores
                .Where(s => s.TournamentId == id && s.RoundNumber == round)
                .ToList();

            ViewBag.TeamScores = teamScores;

            // ✅ Pass round AFTER query (important for view consistency)
            ViewBag.CurrentRound = round;

            return View(tournament);
        }


        [HttpPost]
        public IActionResult RemovePlayer(int tournamentId, int playerId)
        {
            var player = _context.TournamentPlayers
                .FirstOrDefault(tp => tp.TournamentId == tournamentId && tp.PlayerId == playerId);

            if (player != null)
            {
                _context.TournamentPlayers.Remove(player);
                _context.SaveChanges();
            }

            return RedirectToAction("ManagePlayers", new { id = tournamentId });
        }

        public async Task<IActionResult> ManageTeams(int id)
        {
            var tournament = await _context.Tournaments
                .Include(t => t.TournamentTeams)
                    .ThenInclude(tt => tt.Team)
                        .ThenInclude(team => team.PlayerTeams)
                            .ThenInclude(pt => pt.Player)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);

            if (user == null || tournament.CreatorId != user.Id)
                return Forbid();

            return View(tournament);
        }

        [HttpPost]
        public async Task<IActionResult> RemovePlayerFromTeam(int teamId, int playerId, int tournamentId)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            var user = await _userManager.GetUserAsync(User);

            if (tournament == null || user == null || tournament.CreatorId != user.Id)
                return Forbid();   // 🔒 BLOCK NON-CREATORS

            var playerTeam = await _context.PlayerTeams
                .FirstOrDefaultAsync(pt => pt.TeamId == teamId && pt.PlayerId == playerId);

            if (playerTeam != null)
            {
                _context.PlayerTeams.Remove(playerTeam);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("ManageTeams", new { id = tournamentId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTeam(int teamId, int tournamentId)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            var user = await _userManager.GetUserAsync(User);

            // 🔒 SECURITY CHECK (CREATOR ONLY)
            if (tournament == null || user == null || tournament.CreatorId != user.Id)
                return Forbid();

            var team = await _context.Teams
                .Include(t => t.PlayerTeams)
                .FirstOrDefaultAsync(t => t.Id == teamId);

            if (team != null)
            {
                // Remove all player-team relationships first
                _context.PlayerTeams.RemoveRange(team.PlayerTeams);

                _context.Teams.Remove(team);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("ManageTeams", new { id = tournamentId });
        }

        [HttpPost]
        public async Task<IActionResult> MovePlayer(int playerId, int currentTeamId, int newTeamId, int tournamentId)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            var user = await _userManager.GetUserAsync(User);

            // 🔒 SECURITY CHECK (CREATOR ONLY)
            if (tournament == null || user == null || tournament.CreatorId != user.Id)
                return Forbid();

            var playerTeam = await _context.PlayerTeams
                .FirstOrDefaultAsync(pt => pt.PlayerId == playerId && pt.TeamId == currentTeamId);

            if (playerTeam != null)
            {
                _context.PlayerTeams.Remove(playerTeam);

                // Prevent duplicate assignment
                var exists = await _context.PlayerTeams
                    .FirstOrDefaultAsync(pt => pt.PlayerId == playerId && pt.TeamId == newTeamId);

                if (exists == null)
                {
                    var newAssignment = new PlayerTeam
                    {
                        PlayerId = playerId,
                        TeamId = newTeamId,
                        TournamentId = tournamentId
                    };

                    _context.PlayerTeams.Add(newAssignment);
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("ManageTeams", new { id = tournamentId });
        }

        [HttpPost]
        [Authorize]
        public IActionResult EnterScrambleScores(int id, int round, List<int> strokes, List<int> teamIds, List<int> holeNumbers)
        {
            for (int i = 0; i < strokes.Count; i++)
            {
                var existingScore = _context.TeamScores.FirstOrDefault(s =>
                    s.TournamentId == id &&
                    s.TeamId == teamIds[i] &&
                    s.HoleNumber == holeNumbers[i] &&
                    s.RoundNumber == round);

                if (existingScore != null)
                {
                    existingScore.Strokes = strokes[i];
                }
                else
                {
                    var score = new TeamScore
                    {
                        TournamentId = id,
                        TeamId = teamIds[i],
                        HoleNumber = holeNumbers[i],
                        RoundNumber = round, // 🔥 CRITICAL
                        Strokes = strokes[i]
                    };

                    _context.TeamScores.Add(score);
                }
            }

            _context.SaveChanges();

            return RedirectToAction("EnterScrambleScores", new { id, round });
        }



        [HttpPost]
        public IActionResult SaveSinglePar([FromBody] SaveParRequest request)
        {
            var existing = _context.TournamentHolePars
                .FirstOrDefault(p => p.TournamentId == request.TournamentId
                                && p.HoleNumber == request.HoleNumber);

            if (existing != null)
            {
                // UPDATE
                existing.Par = request.Par;
            }
            else
            {
                // CREATE
                _context.TournamentHolePars.Add(new TournamentHolePar
                {
                    TournamentId = request.TournamentId,
                    HoleNumber = request.HoleNumber,
                    Par = request.Par
                });
            }

            _context.SaveChanges();

            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult SaveScrambleHole(int tournamentId, int teamId, int holeNumber, int strokes, int roundNumber)
        {
            var existing = _context.TeamScores.FirstOrDefault(s =>
                s.TournamentId == tournamentId &&
                s.TeamId == teamId &&
                s.HoleNumber == holeNumber &&
                s.RoundNumber == roundNumber);

            if (existing != null)
            {
                existing.Strokes = strokes;
            }
            else
            {
                _context.TeamScores.Add(new TeamScore
                {
                    TournamentId = tournamentId,
                    TeamId = teamId,
                    HoleNumber = holeNumber,
                    Strokes = strokes,
                    RoundNumber = roundNumber
                });
            }

            _context.SaveChanges();

            return Json(new { success = true });
        }

        public IActionResult History()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var player = _context.Players.FirstOrDefault(p => p.UserId == userId);

            if (player == null)
                return NotFound();

            var tournaments = _context.TournamentPlayers
                .Where(tp => tp.PlayerId == player.Id)
                .Select(tp => tp.Tournament)
                .ToList();

            var allScores = _context.Scores
                .Where(s => s.PlayerId == player.Id)
                .ToList();

            var pars = _context.TournamentHolePars.ToList();

            var history = tournaments
                .Select(t =>
                {
                    var scores = allScores.Where(s => s.TournamentId == t.Id).ToList();

                    int totalStrokes = scores.Sum(s => s.Strokes);

                    int parPerRound = pars
                        .Where(p => p.TournamentId == t.Id)
                        .Sum(p => p.Par);

                    int roundsPlayed = scores.Count / t.HoleCount;

                    int totalPar = parPerRound * roundsPlayed;

                    int toPar = totalStrokes - totalPar;

                    // 🔥 Calculate rank
                    var tournamentScores = _context.Scores
                        .Where(s => s.TournamentId == t.Id)
                        .ToList();

                    var leaderboard = tournamentScores
                        .GroupBy(s => s.PlayerId)
                        .Select(g => new
                        {
                            PlayerId = g.Key,
                            Total = g.Sum(x => x.Strokes)
                        })
                        .OrderBy(x => x.Total)
                        .ToList();

                    int rank = leaderboard.FindIndex(x => x.PlayerId == player.Id) + 1;

                    return new
                    {
                        TournamentId = t.Id,
                        TournamentName = t.Name,
                        Date = t.Date,
                        ToPar = toPar,
                        Rank = rank
                    };
                })
                .OrderByDescending(x => x.Date)
                .ToList();

            // 🔥 SUMMARY STATS
            int tournamentsPlayed = history.Count;

            double avgToPar = tournamentsPlayed > 0
                ? history.Average(x => x.ToPar)
                : 0;

            int bestFinish = tournamentsPlayed > 0
                ? history.Min(x => x.Rank)
                : 0;

            int totalRounds = tournamentsPlayed;

            ViewBag.TournamentsPlayed = tournamentsPlayed;
            ViewBag.AvgToPar = Math.Round(avgToPar, 1);
            ViewBag.BestFinish = bestFinish;

            return View(history);
        }

    
        public IActionResult Leaderboard(int id, int? round)
        {
            var tournament = _context.Tournaments
                .Include(t => t.TournamentPlayers)
                    .ThenInclude(tp => tp.Player)
                .Include(t => t.TournamentTeams)
                    .ThenInclude(tt => tt.Team)
                        .ThenInclude(team => team.PlayerTeams)
                .FirstOrDefault(t => t.Id == id);

            if (tournament == null)
                return NotFound();

            int selectedRound = round ?? 0; // 0 = total

            ViewBag.SelectedRound = selectedRound;
            ViewBag.TotalRounds = tournament.NumberOfRounds;

            var allScores = _context.Scores
                .Where(s => s.TournamentId == id)
                .ToList();

            var pars = _context.TournamentHolePars
                .Where(p => p.TournamentId == id)
                .ToList();

            int totalHolesAllRounds = tournament.HoleCount * tournament.NumberOfRounds;

            List<object> leaderboard;

            if (tournament.IsTeamBased)
            {
                leaderboard = tournament.TournamentTeams
                    .Select(tt =>
                    {
                        var teamPlayerIds = tt.Team.PlayerTeams
                            .Select(pt => pt.PlayerId)
                            .ToList();

                        var teamPlayerScores = allScores
                            .Where(s => teamPlayerIds.Contains(s.PlayerId))
                            .ToList();

                        List<(int strokes, int round)> scoresByHole = new List<(int, int)>();

                        if (tournament.ScoringType == "BestBall")
                        {
                            scoresByHole = teamPlayerScores
                                .GroupBy(s => new { s.HoleNumber, s.RoundNumber })
                                .Select(g => (g.Min(x => x.Strokes), g.Key.RoundNumber))
                                .ToList();
                        }
                        else if (tournament.ScoringType == "StrokePlay")
                        {
                            scoresByHole = teamPlayerScores
                                .GroupBy(s => new { s.HoleNumber, s.RoundNumber })
                                .Select(g => (g.Sum(x => x.Strokes), g.Key.RoundNumber))
                                .ToList();
                        }
                        else if (tournament.ScoringType == "Scramble")
                        {
                            var scrambleScores = _context.TeamScores
                                .Where(s => s.TournamentId == id && s.TeamId == tt.TeamId)
                                .ToList();

                            scoresByHole = scrambleScores
                                .Select(s => (s.Strokes, s.RoundNumber))
                                .ToList();
                        }

                        if (selectedRound > 0)
                            scoresByHole = scoresByHole
                                .Where(s => s.round == selectedRound)
                                .ToList();

                        int totalStrokes = scoresByHole.Sum(s => s.strokes);

                        int holesPlayed = scoresByHole.Count;

                        

                        int parPerRound = pars.Sum(p => p.Par);

                        int roundsCompleted = holesPlayed / tournament.HoleCount;

                        int parSoFar = selectedRound == 0
                            ? parPerRound * roundsCompleted
                            : pars.Take(holesPlayed).Sum(p => p.Par);

                        string thruDisplay = holesPlayed == 0
                            ? "-"
                            : (selectedRound == 0
                                ? (holesPlayed == totalHolesAllRounds ? "F" : $"Thru {holesPlayed}")
                                : (holesPlayed == tournament.HoleCount ? "F" : $"Thru {holesPlayed}"));

                        return new
                        {
                            Name = tt.Team.Name,
                            Total = totalStrokes,
                            ToPar = totalStrokes - parSoFar,
                            Thru = thruDisplay
                        };
                    })
                    .OrderBy(x => x.Total)
                    .Select((x, index) => new
                    {
                        Rank = index + 1,
                        x.Name,
                        x.Total,
                        x.ToPar,
                        x.Thru
                    })
                    .Cast<object>()
                    .ToList();
            }
            else
            {
                leaderboard = tournament.TournamentPlayers
                    .Select(tp =>
                    {
                        var playerScores = allScores
                            .Where(s => s.PlayerId == tp.PlayerId)
                            .ToList();

                        if (selectedRound > 0)
                            playerScores = playerScores
                                .Where(s => s.RoundNumber == selectedRound)
                                .ToList();

                        int totalStrokes = playerScores.Sum(s => s.Strokes);

                        int holesPlayed = playerScores.Count;

                       
                        int parPerRound = pars.Sum(p => p.Par);

                        int roundsCompleted = holesPlayed / tournament.HoleCount;

                        int parSoFar = selectedRound == 0
                            ? parPerRound * roundsCompleted
                            : pars.Take(holesPlayed).Sum(p => p.Par);

                        string thruDisplay = holesPlayed == 0
                            ? "-"
                            : (selectedRound == 0
                                ? (holesPlayed == totalHolesAllRounds ? "F" : $"Thru {holesPlayed}")
                                : (holesPlayed == tournament.HoleCount ? "F" : $"Thru {holesPlayed}"));

                        return new
                        {
                            Name = tp.Player.Name,
                            Total = totalStrokes,
                            ToPar = totalStrokes - parSoFar,
                            Thru = thruDisplay
                        };
                    })
                    .OrderBy(x => x.Total)
                    .Select((x, index) => new
                    {
                        Rank = index + 1,
                        x.Name,
                        x.Total,
                        x.ToPar,
                        x.Thru
                    })
                    .Cast<object>()
                    .ToList();
            }

            ViewBag.TournamentName = tournament.Name;
            ViewBag.CourseName = tournament.CourseName;
            ViewBag.Date = tournament.Date.ToString("MMM dd, yyyy");

            string scoringDisplay = tournament.IsTeamBased
                ? tournament.ScoringType
                : "Individual";

            ViewBag.ScoringType = scoringDisplay;
            ViewBag.Tournament = tournament;
            return View(leaderboard);
        }



        // SAVE PLAYER SCORES
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveScores(int TournamentId, int PlayerId, Dictionary<int, int> Scores)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var player = _context.Players.FirstOrDefault(p => p.Id == PlayerId);

            if (player == null)
                return NotFound();

            if (player.UserId != currentUserId)
                return Unauthorized();

            foreach (var hole in Scores)
            {
                int holeNumber = hole.Key;
                int strokes = hole.Value;

                var existingScore = _context.Scores
                    .FirstOrDefault(s =>
                        s.TournamentId == TournamentId &&
                        s.PlayerId == PlayerId &&
                        s.HoleNumber == holeNumber);

                if (existingScore != null)
                {
                    // ✅ UPDATE existing score
                    existingScore.Strokes = strokes;
                }
                else
                {
                    // ✅ INSERT new score
                    var score = new Score
                    {
                        TournamentId = TournamentId,
                        PlayerId = PlayerId,
                        HoleNumber = holeNumber,
                        Strokes = strokes
                    };

                    _context.Scores.Add(score);
                }
            }

            _context.SaveChanges();

            return RedirectToAction("Details", new { id = TournamentId });
        }

        [HttpPost]
        public IActionResult SaveHoleScore(int tournamentId, int playerId, int holeNumber, int strokes, int roundNumber)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var player = _context.Players.FirstOrDefault(p => p.Id == playerId);

            if (player == null)
                return NotFound();

            if (player.UserId != currentUserId)
                return Unauthorized();

            var existingScore = _context.Scores.FirstOrDefault(s =>
                s.TournamentId == tournamentId &&
                s.PlayerId == playerId &&
                s.HoleNumber == holeNumber &&
                s.RoundNumber == roundNumber);

            if (existingScore != null)
            {
                existingScore.Strokes = strokes;
            }
            else
            {
                _context.Scores.Add(new Score
                {
                    TournamentId = tournamentId,
                    PlayerId = playerId,
                    HoleNumber = holeNumber,
                    Strokes = strokes,
                    RoundNumber = roundNumber
                });
            }

            _context.SaveChanges();

            return Json(new { success = true });
        }

        // SAVE TEAM SCORES
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveScrambleScores(int TournamentId, int TeamId, Dictionary<int, int> Scores)
        {
            foreach (var hole in Scores)
            {
                int holeNumber = hole.Key;
                int strokes = hole.Value;

                var existingScore = _context.TeamScores
                    .FirstOrDefault(s =>
                        s.TournamentId == TournamentId &&
                        s.TeamId == TeamId &&
                        s.HoleNumber == holeNumber);

                if (existingScore != null)
                {
                    existingScore.Strokes = strokes;
                }
                else
                {
                    var score = new TeamScore
                    {
                        TournamentId = TournamentId,
                        TeamId = TeamId,
                        HoleNumber = holeNumber,
                        Strokes = strokes
                    };

                    _context.TeamScores.Add(score);
                }
            }

            _context.SaveChanges();

            return RedirectToAction("Details", new { id = TournamentId });
        }

        public class NominatimResult
        {
            public string lat { get; set; }
            public string lon { get; set; }
        }
        private string GenerateJoinCode()
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}