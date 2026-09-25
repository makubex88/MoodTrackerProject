using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace MoodTrackerProject.Data.Migrations;

[DbContext(typeof(MoodDbContext))]
partial class MoodDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 64);

        MySqlModelBuilderExtensions.AutoIncrementColumns(modelBuilder);

        modelBuilder.Entity("MoodTrackerProject.Data.Entities.MoodEntry", b =>
        {
            b.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint")
                .HasColumnName("id");

            MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<long>("Id"));

            b.Property<string>("Comment")
                .HasMaxLength(500)
                .HasColumnType("varchar(500)")
                .HasColumnName("comment");

            b.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("datetime(6)")
                .HasColumnName("created_at_utc");

            b.Property<DateOnly>("EntryDate")
                .HasColumnType("date")
                .HasColumnName("entry_date");

            b.Property<Guid>("ParticipantId")
                .HasColumnType("char(36)")
                .HasColumnName("participant_id")
                .UseCollation("ascii_general_ci");

            b.Property<byte>("Rating")
                .HasColumnType("tinyint unsigned")
                .HasColumnName("rating");

            b.HasKey("Id");

            b.HasIndex("CreatedAtUtc")
                .HasDatabaseName("ix_created_at");

            b.HasIndex("ParticipantId", "EntryDate")
                .IsUnique()
                .HasDatabaseName("ux_participant_day");

            b.ToTable("mood_entries", (string)null);
        });
#pragma warning restore 612, 618
    }
}
