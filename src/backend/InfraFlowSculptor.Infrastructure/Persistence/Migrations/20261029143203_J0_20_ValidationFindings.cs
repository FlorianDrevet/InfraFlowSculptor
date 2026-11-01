using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1707, CA1861

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_20_ValidationFindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "project_environments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "finding_acknowledgments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finding_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    object_version = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    acknowledged_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_finding_acknowledgments", x => x.id);
                    table.ForeignKey(
                        name: "fk_finding_acknowledgments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_finding_acknowledgments_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_finding_acknowledgments_user_profiles_acknowledged_by",
                        column: x => x.acknowledged_by,
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_finding_acknowledgments_acknowledged_by",
                table: "finding_acknowledgments",
                column: "acknowledged_by");

            migrationBuilder.CreateIndex(
                name: "ix_finding_acknowledgments_active_lookup",
                table: "finding_acknowledgments",
                columns: new[] { "project_id", "finding_code", "subject_type", "subject_id", "environment_id", "object_version", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_finding_acknowledgments_organization_id",
                table: "finding_acknowledgments",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finding_acknowledgments");

            migrationBuilder.DropColumn(
                name: "version",
                table: "project_environments");
        }
    }
}
