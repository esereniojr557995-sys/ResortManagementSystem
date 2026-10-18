using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResortManagementSystem.Infrastructure.Migrations.TenantResortDb
{
    /// <inheritdoc />
    public partial class AddArchiveToAllEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Staff",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Reservations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Promotions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Promotions");
        }
    }
}
