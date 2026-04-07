using System;
using System.ComponentModel.DataAnnotations;

namespace FairwayManager.Models
{
    public class TournamentTeam
    {
        public int TournamentId { get; set; }
        public Tournament? Tournament { get; set; }

        public int TeamId { get; set; }
        public Team? Team { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}