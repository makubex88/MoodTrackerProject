using Microsoft.EntityFrameworkCore;
using MoodTrackerProject.Data.Entities;

namespace MoodTrackerProject.Data;

public sealed class MoodDbContext(DbContextOptions<MoodDbContext> options) : DbContext(options)
{
    public const string ParticipantDayIndex = "ux_participant_day";
    public const string CreatedAtIndex = "ix_created_at";

    public DbSet<MoodEntry> MoodEntries => Set<MoodEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MoodEntry>(e =>
        {
            e.ToTable("mood_entries");

            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");

            // Guid maps to char(36) in Pomelo; the driver round-trips it as a Guid (GuidFormat=Char36).
            e.Property(x => x.ParticipantId)
                .HasColumnName("participant_id")
                .HasColumnType("char(36)");

            e.Property(x => x.Rating)
                .HasColumnName("rating")
                .HasConversion<byte>();

            e.Property(x => x.Comment)
                .HasColumnName("comment")
                .HasMaxLength(500);

            e.Property(x => x.EntryDate)
                .HasColumnName("entry_date");

            e.Property(x => x.CreatedAtUtc)
                .HasColumnName("created_at_utc");

            // The requirement, expressed where it cannot be bypassed.
            e.HasIndex(x => new { x.ParticipantId, x.EntryDate })
                .IsUnique()
                .HasDatabaseName(ParticipantDayIndex);

            // Admin list ordering.
            e.HasIndex(x => x.CreatedAtUtc)
                .HasDatabaseName(CreatedAtIndex);
        });
    }
}
