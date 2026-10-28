using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1707, CA1861

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_13_NameAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "name_availability",
                columns: table => new
                {
                    availability_domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    name = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    failure_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_name_availability", x => new { x.availability_domain, x.name });
                    table.CheckConstraint("ck_name_availability_attempts", "attempts >= 0 AND attempts <= 3");
                    table.CheckConstraint("ck_name_availability_status", "status IN ('Pending', 'Resolved', 'NotResolved', 'Unknown')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_name_availability_status_next_attempt_at",
                table: "name_availability",
                columns: new[] { "status", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "name_availability");
        }
    }
}
