using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoodTrackerProject.Data.Migrations;

[DbContext(typeof(MoodDbContext))]
[Migration("20260923000000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "mood_entries",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                participant_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                rating = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                comment = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                entry_date = table.Column<DateOnly>(type: "date", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mood_entries", x => x.id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "ix_created_at",
            table: "mood_entries",
            column: "created_at_utc");

        migrationBuilder.CreateIndex(
            name: "ux_participant_day",
            table: "mood_entries",
            columns: new[] { "participant_id", "entry_date" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "mood_entries");
    }
}
