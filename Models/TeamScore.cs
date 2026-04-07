using System.ComponentModel.DataAnnotations;

namespace FairwayManager.Models
{
    public class TeamScore
    {
        public int Id { get; set; }

        [Required]
        public int TournamentId { get; set; }

        [Required]
        public int TeamId { get; set; }

        [Required]
        public int HoleNumber { get; set; }

        [Required]
        public int Strokes { get; set; }
        
        public int RoundNumber { get; set; }
    }
}