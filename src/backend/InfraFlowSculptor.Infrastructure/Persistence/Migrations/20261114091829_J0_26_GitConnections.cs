using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1707, CA1861

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_26_GitConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "git_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_kind = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    organization = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    authentication_mode = table.Column<int>(type: "integer", nullable: false),
                    personal_access_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    secret_name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    expiry_15_day_alert_sent = table.Column<bool>(type: "boolean", nullable: false),
                    expiry_3_day_alert_sent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_git_connections", x => x.id);
                    table.ForeignKey(
                        name: "fk_git_connections_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "git_connection_publication_uses",
                columns: table => new
                {
                    connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_git_connection_publication_uses", x => new { x.connection_id, x.project_id });
                    table.ForeignKey(
                        name: "fk_git_connection_publication_uses_git_connections_connection_",
                        column: x => x.connection_id,
                        principalTable: "git_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_git_connection_publication_uses_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "git_connection_repositories",
                columns: table => new
                {
                    provider_repository_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    git_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    default_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    open_to_all_projects = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_git_connection_repositories", x => new { x.git_connection_id, x.provider_repository_id });
                    table.ForeignKey(
                        name: "fk_git_connection_repositories_git_connections_git_connection_",
                        column: x => x.git_connection_id,
                        principalTable: "git_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "git_connection_repository_projects",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    git_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_repository_id = table.Column<string>(type: "character varying(255)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_git_connection_repository_projects", x => new { x.git_connection_id, x.provider_repository_id, x.project_id });
                    table.ForeignKey(
                        name: "fk_git_connection_repository_projects_git_connection_repositor",
                        columns: x => new { x.git_connection_id, x.provider_repository_id },
                        principalTable: "git_connection_repositories",
                        principalColumns: new[] { "git_connection_id", "provider_repository_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_git_connection_publication_uses_project_id",
                table: "git_connection_publication_uses",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_git_connections_organization_id",
                table: "git_connections",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "git_connection_publication_uses");

            migrationBuilder.DropTable(
                name: "git_connection_repository_projects");

            migrationBuilder.DropTable(
                name: "git_connection_repositories");

            migrationBuilder.DropTable(
                name: "git_connections");
        }
    }
}
