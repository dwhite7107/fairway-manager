using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FairwayManager.Models;

public class Hole
{
    public int Id { get; set; }

    public int TournamentId { get; set; }

    public int HoleNumber { get; set; }

    public int Par { get; set; }

    // Navigation
    public Tournament Tournament { get; set; }
}