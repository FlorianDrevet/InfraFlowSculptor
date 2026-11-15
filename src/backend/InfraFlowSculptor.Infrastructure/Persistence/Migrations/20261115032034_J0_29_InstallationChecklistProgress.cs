using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1707, CA1861

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_29_InstallationChecklistProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "installation_checklist_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installation_checklist_progress", x => x.id);
                    table.ForeignKey(
                        name: "fk_installation_checklist_progress_organizations_organization_",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_installation_checklist_progress_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_installation_checklist_progress_user_profiles_completed_by",
                        column: x => x.completed_by,
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_installation_checklist_progress_completed_by",
                table: "installation_checklist_progress",
                column: "completed_by");

            migrationBuilder.CreateIndex(
                name: "ix_installation_checklist_progress_organization_id",
                table: "installation_checklist_progress",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ux_installation_checklist_progress_project_item",
                table: "installation_checklist_progress",
                columns: new[] { "project_id", "item_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "installation_checklist_progress");
        }
    }
}
