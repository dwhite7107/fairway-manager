using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace FairwayManager.Models
{
    public class Player
    {
        public int Id { get; set; }

        // Link to Identity user
        public string? UserId { get; set; }
        public IdentityUser? User { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties for bridge tables
        public ICollection<TournamentPlayer>? TournamentPlayers { get; set; }

        public ICollection<PlayerTeam>? PlayerTeams { get; set; }

        public ICollection<Score>? Scores { get; set; }
        public string? PhoneNumber { get; set; }
        public string? HomeCourse { get; set; }
    }
}