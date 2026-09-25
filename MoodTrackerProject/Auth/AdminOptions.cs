using System.ComponentModel.DataAnnotations;

namespace MoodTrackerProject.Auth;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>
    /// The single configured admin credential. Comes from configuration (Admin__Password in compose),
    /// never from source. The local default is deliberately non-secret and named as such in the README.
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>Sliding lifetime of the admin ticket cookie.</summary>
    [Range(1, 24 * 7)]
    public int SessionHours { get; set; } = 8;
}
