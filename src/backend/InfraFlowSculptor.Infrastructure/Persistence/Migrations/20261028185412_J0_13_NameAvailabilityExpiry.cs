using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1707, CA1861

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_13_NameAvailabilityExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_name_availability_expires_at",
                table: "name_availability",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_name_availability_expires_at",
                table: "name_availability");
        }
    }
}
