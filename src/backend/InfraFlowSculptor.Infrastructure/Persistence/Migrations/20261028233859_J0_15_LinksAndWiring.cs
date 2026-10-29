using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1707, CA1861

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_15_LinksAndWiring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "default_log_analytics_resource_id",
                table: "components",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "project_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    identity_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    identity_resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    access_scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    data_access_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_links_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_link_roles",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    link_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_link_roles", x => new { x.link_id, x.name });
                    table.ForeignKey(
                        name: "fk_project_link_roles_project_links_link_id",
                        column: x => x.link_id,
                        principalTable: "project_links",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_links_source",
                table: "project_links",
                columns: new[] { "project_id", "source_resource_id" });

            migrationBuilder.CreateIndex(
                name: "ix_project_links_target",
                table: "project_links",
                columns: new[] { "project_id", "target_resource_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_link_roles");

            migrationBuilder.DropTable(
                name: "project_links");

            migrationBuilder.DropColumn(
                name: "default_log_analytics_resource_id",
                table: "components");
        }
    }
}
