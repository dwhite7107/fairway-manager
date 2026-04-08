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

        // GET: Tournament
        public async Task<IActionResult> Index()
        {
            var tournaments = await _tournamentService.GetAllTournamentsAsync();
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
            if (!ModelState.IsValid)
                return View(tournament);

            if (!tournament.IsTeamBased)
            {
                tournament.ScoringType = "StrokePlay";
            }

            var user = await _userManager.GetUserAsync(User);
            tournament.CreatorId = user!.Id;

            await _tournamentService.CreateTournamentAsync(tournament);

            return RedirectToAction(nameof(Index));
        }

        [AllowAnonymous]
        public IActionResult Browse(string statusFilter, DateTime? startDate, DateTime? endDate, int page = 1)
        {
            int pageSize = 6;

            var tournaments = _context.Tournaments
                .Include(t => t.TournamentPlayers)
                .ToList();

            var today = DateTime.Today;

            var result = tournaments.Select(t =>
            {
                string status;

                if (t.Date > today)
                    status = "Upcoming";
                else if (t.Date == today)
                    status = "In Progress";
                else
                    status = "Completed";

                return new
                {
                    t.Id,
                    t.Name,
                    t.Date,
                    PlayerCount = t.TournamentPlayers.Count,
                    Status = status
                };
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
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

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
                .OrderBy(p => p.HoleNumber)
                .Select(p => p.Par)
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
        public IActionResult EnterScores(int id)
        {
            var tournament = _context.Tournaments
                .Include(t => t.TournamentPlayers)
                    .ThenInclude(tp => tp.Player)
                .Include(t => t.TournamentTeams)
                    .ThenInclude(tt => tt.Team)
                .FirstOrDefault(t => t.Id == id);

            if (tournament == null)
                return NotFound();

            if (tournament.ScoringType == "Scramble")
            {
                return RedirectToAction("EnterScrambleScores", new { id });
            }

            
            var scores = _context.Scores
                .Where(s => s.TournamentId == id)
                .ToList();

            
            ViewBag.TournamentPlayers = tournament.TournamentPlayers;


            ViewBag.Scores = scores;



            return View(tournament);
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

                    int par = pars
                        .Where(p => p.TournamentId == t.Id &&
                                    scores.Select(s => s.HoleNumber).Contains(p.HoleNumber))
                        .Sum(p => p.Par);

                    int toPar = totalStrokes - par;

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

    
        public IActionResult Leaderboard(int id)
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

            var allScores = _context.Scores
                .Where(s => s.TournamentId == id)
                .ToList();

            var pars = _context.TournamentHolePars
                .Where(p => p.TournamentId == id)
                .ToList();

            int parPerRound = pars.Sum(p => p.Par);
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

                        List<int> scoresByHole = new List<int>();

                        if (tournament.ScoringType == "BestBall")
                        {
                            scoresByHole = teamPlayerScores
                                .GroupBy(s => new { s.HoleNumber, s.RoundNumber })
                                .Select(g => g.Min(x => x.Strokes))
                                .ToList();
                        }
                        else if (tournament.ScoringType == "Stroke")
                        {
                            scoresByHole = teamPlayerScores
                                .GroupBy(s => new { s.HoleNumber, s.RoundNumber })
                                .Select(g => g.Sum(x => x.Strokes))
                                .ToList();
                        }
                        else if (tournament.ScoringType == "Scramble")
                        {
                            var scrambleScores = _context.TeamScores
                                .Where(s => s.TournamentId == id && s.TeamId == tt.TeamId)
                                .ToList();

                            scoresByHole = scrambleScores
                                .Select(s => s.Strokes)
                                .ToList();
                        }

                        int totalStrokes = scoresByHole.Sum();

                        int holesPlayed = scoresByHole.Count;
                        int roundsCompleted = holesPlayed / tournament.HoleCount;
                        int parSoFar = parPerRound * roundsCompleted;

                        string thruDisplay = holesPlayed == totalHolesAllRounds
                            ? "F"
                            : $"Thru {holesPlayed}";

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

                        int totalStrokes = playerScores.Sum(s => s.Strokes);

                        int holesPlayed = playerScores.Count;
                        int roundsCompleted = holesPlayed / tournament.HoleCount;
                        int parSoFar = parPerRound * roundsCompleted;

                        string thruDisplay = holesPlayed == totalHolesAllRounds
                            ? "F"
                            : $"Thru {holesPlayed}";

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
    }
}