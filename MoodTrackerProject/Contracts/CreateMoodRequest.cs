using System.ComponentModel.DataAnnotations;

namespace MoodTrackerProject.Contracts;

/// <summary>Body of POST /api/moods. The wire format is the number 1–4, never the label.</summary>
public sealed class CreateMoodRequest
{
    [Required]
    [Range(1, 4, ErrorMessage = "Rating must be between 1 (Not good at all) and 4 (Feeling great).")]
    public int? Rating { get; init; }

    [MaxLength(500, ErrorMessage = "Comments are limited to 500 characters.")]
    public string? Comment { get; init; }
}
