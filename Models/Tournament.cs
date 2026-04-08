using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using FairwayManager.Models;

namespace FairwayManager.Models
{
    public class Tournament
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public string CourseName { get; set; } = string.Empty;


        [Required]
        public DateTime Date { get; set; }

        public string Status { get; set; } = "Open";

        [Required]
        public int HoleCount { get; set; } // 9 or 18

        public int NumberOfRounds { get; set; } = 1;

        public bool IsTeamBased { get; set; }

        public string? ScoringType { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsLocked { get; set; } = false;

        public string JoinCode { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Tournament creator
        public string? CreatorId { get; set; }
        public IdentityUser? Creator { get; set; }

        // Bridge relationships
        public ICollection<TournamentPlayer>? TournamentPlayers { get; set; }

        public ICollection<TournamentTeam>? TournamentTeams { get; set; }

        public ICollection<Score>? Scores { get; set; }
        public int PlayerCount => TournamentPlayers?.Count ?? 0;
    }
}