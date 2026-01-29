using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PbesApi.Migrations;

public partial class CreateOfficers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Officers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ServiceNumber = table.Column<string>(type: "text", nullable: false),
                Email = table.Column<string>(type: "text", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: false),
                Role = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Officers", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Officers_ServiceNumber",
            table: "Officers",
            column: "ServiceNumber",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Officers");
    }
}
