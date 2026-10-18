using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResortManagementSystem.Infrastructure.Migrations.TenantResortDb
{
    /// <inheritdoc />
    public partial class AddCategoryEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EligibleCategory",
                table: "Promotions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerCategory",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EligibleCategory",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "CustomerCategory",
                table: "Customers");
        }
    }
}
