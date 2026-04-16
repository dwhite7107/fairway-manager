using System;

namespace FairwayManager.Models
{
    public class TournamentActivity
    {
        public int Id { get; set; }

        public int TournamentId { get; set; }

        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}