using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FairwayManager.Data;
using FairwayManager.Models;

namespace FairwayManager.Controllers
{
    [Authorize]
    public class TeamController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public TeamController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ----------------------------
        // Create Team Page
        // ----------------------------
        public IActionResult Create(int tournamentId)
        {
            ViewBag.TournamentId = tournamentId;
            return View();
        }

        // ----------------------------
        // Create Team
        // ----------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int tournamentId, string name)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized();

            var player = await _context.Players
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (player == null)
                return BadRequest("You must join the tournament first.");

            // Create team
            var team = new Team
            {
                Name = name,
                CaptainPlayerId = player.Id
            };

            _context.Teams.Add(team);
            await _context.SaveChangesAsync();

            // Add captain to PlayerTeam
            var playerTeam = new PlayerTeam
            {
                PlayerId = player.Id,
                TeamId = team.Id,
                TournamentId = tournamentId
            };

            _context.PlayerTeams.Add(playerTeam);

            // Link team to tournament
            var tournamentTeam = new TournamentTeam
            {
                TournamentId = tournamentId,
                TeamId = team.Id
            };

            _context.TournamentTeams.Add(tournamentTeam);

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Tournament", new { id = tournamentId });
        }

        // ----------------------------
        // Join Team
        // ----------------------------
        [HttpPost]
        public async Task<IActionResult> Join(int teamId, int tournamentId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized();

            var player = await _context.Players
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (player == null)
                return BadRequest("You must join the tournament first.");

            // Prevent joining multiple teams
            var alreadyOnTeam = await _context.PlayerTeams
                .AnyAsync(pt => pt.PlayerId == player.Id && pt.TournamentId == tournamentId);

            if (alreadyOnTeam)
                return RedirectToAction("Details", "Tournament", new { id = tournamentId });

            var playerTeam = new PlayerTeam
            {
                PlayerId = player.Id,
                TeamId = teamId,
                TournamentId = tournamentId
            };

            _context.PlayerTeams.Add(playerTeam);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Tournament", new { id = tournamentId });
        }
    }
}