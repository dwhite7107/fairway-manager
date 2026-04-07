using System.ComponentModel.DataAnnotations;

namespace FairwayManager.Models
{
    public class TournamentHolePar
    {
        public int Id { get; set; }

        public int TournamentId { get; set; }

        public int HoleNumber { get; set; }

        public int Par { get; set; }

        public Tournament? Tournament { get; set; }
    }
}