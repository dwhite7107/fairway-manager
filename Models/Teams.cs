using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FairwayManager.Models
{
    public class Team
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        // Player who created the team
        public int CaptainPlayerId { get; set; }
        public Player? CaptainPlayer { get; set; }

        // Code other players use to join the team
        public string TeamJoinCode { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Bridge relationships
        public ICollection<PlayerTeam>? PlayerTeams { get; set; }

        public ICollection<TournamentTeam>? TournamentTeams { get; set; }
    }
}