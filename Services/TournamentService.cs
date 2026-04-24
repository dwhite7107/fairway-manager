using FairwayManager.Data;
using FairwayManager.Models;
using Microsoft.EntityFrameworkCore;

namespace FairwayManager.Services
{
    public class TournamentService
    {
        private readonly ApplicationDbContext _context;

        public TournamentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Tournament>> GetAllTournamentsAsync()
        {
            return await _context.Tournaments
                .Include(t => t.TournamentPlayers)
                .ToListAsync();
        }

        public async Task<Tournament?> GetTournamentByIdAsync(int id)
        {
            return await _context.Tournaments
            .Include(t => t.TournamentPlayers)
                .ThenInclude(tp => tp.Player)
            .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task CreateTournamentAsync(Tournament tournament)
        {
            tournament.Status = "Open";
            tournament.CreatedAt = DateTime.UtcNow;

            
            tournament.Date = tournament.Date.ToUniversalTime();

            // Generate a UNIQUE join code
            tournament.JoinCode = await GenerateUniqueJoinCode();

            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();
        }

        private async Task<string> GenerateUniqueJoinCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            string code;

            do
            {
                code = new string(Enumerable.Repeat(chars, 6)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
            }
            while (await _context.Tournaments.AnyAsync(t => t.JoinCode == code));

            return code;
        }
    }
}