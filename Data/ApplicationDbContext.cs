using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FairwayManager.Models;

namespace FairwayManager.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Core Tables
        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<Score> Scores { get; set; }
        public DbSet<TournamentHolePar> TournamentHolePars { get; set; }
        public DbSet<TeamScore> TeamScores { get; set; }

        // Bridge Tables
        public DbSet<TournamentPlayer> TournamentPlayers { get; set; }
        public DbSet<TournamentTeam> TournamentTeams { get; set; }
        public DbSet<PlayerTeam> PlayerTeams { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Composite key for TournamentPlayers
            modelBuilder.Entity<TournamentPlayer>()
                .HasKey(tp => new { tp.TournamentId, tp.PlayerId });

            // Composite key for TournamentTeams
            modelBuilder.Entity<TournamentTeam>()
                .HasKey(tt => new { tt.TournamentId, tt.TeamId });

            // Composite key for PlayerTeams
            modelBuilder.Entity<PlayerTeam>()
                .HasKey(pt => new { pt.TournamentId, pt.PlayerId });

            // Prevent duplicate hole scores
            modelBuilder.Entity<Score>()
                .HasIndex(s => new { s.TournamentId, s.PlayerId, s.HoleNumber })
                .IsUnique();
        }
    }
}