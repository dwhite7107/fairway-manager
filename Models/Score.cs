using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FairwayManager.Models
{
    [Index(nameof(TournamentId), nameof(PlayerId), nameof(HoleNumber), nameof(RoundNumber), IsUnique = true)]
    public class Score
    {
        public int Id { get; set; }

        [Required]
        public int TournamentId { get; set; }
        public Tournament? Tournament { get; set; }

        [Required]
        public int PlayerId { get; set; }
        public Player? Player { get; set; }
    

        [Required]
        public int HoleNumber { get; set; }

        [Required]
        public int Strokes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int RoundNumber { get; set; }
    }
}