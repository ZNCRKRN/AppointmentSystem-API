using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppointmentSystem.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvisorIsApproved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pre-existing advisor rows (seeded or admin-created) are approved; only a newly
            // self-registered advisor (set explicitly in code) starts unapproved.
            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Advisors",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Advisors");
        }
    }
}
