using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [SuppressMessage("Naming", "CA1707", Justification = "The migration identifier follows the J0-08 plan ID.")]
    public partial class J0_08_ProjectWizard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "default_name_template",
                table: "projects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "{abbr}-{project}-{name}-{env}");

            migrationBuilder.AddColumn<string>(
                name: "naming_preset",
                table: "projects",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Compact");

            migrationBuilder.AddColumn<string>(
                name: "publishing_preset",
                table: "projects",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resource_group_name_template",
                table: "projects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "rg-{project}-{component}-{name}-{env}");

            migrationBuilder.CreateTable(
                name: "project_wizard_drafts",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_json = table.Column<string>(type: "jsonb", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_wizard_drafts", x => new { x.organization_id, x.user_profile_id });
                    table.ForeignKey(
                        name: "fk_project_wizard_drafts_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_project_wizard_drafts_user_profiles_user_profile_id",
                        column: x => x.user_profile_id,
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_wizard_drafts_expires_at",
                table: "project_wizard_drafts",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_project_wizard_drafts_user_profile_id",
                table: "project_wizard_drafts",
                column: "user_profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_wizard_drafts");

            migrationBuilder.DropColumn(
                name: "default_name_template",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "naming_preset",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "publishing_preset",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "resource_group_name_template",
                table: "projects");
        }
    }
}
