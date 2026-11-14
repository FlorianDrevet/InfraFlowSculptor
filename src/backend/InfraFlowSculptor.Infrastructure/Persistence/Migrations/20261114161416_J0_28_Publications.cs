using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1707, CA1861

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_28_Publications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "publications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    author_tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    prepared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publications", x => x.id);
                    table.ForeignKey(
                        name: "fk_publications_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_publications_revisions_revision_id",
                        column: x => x.revision_id,
                        principalTable: "revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "publication_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    publication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    git_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_repository_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    repository_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    default_branch = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    base_commit_sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_branch = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    source_branch = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    commit_sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    pull_request_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pull_request_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    merge_commit_sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    pull_request_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requires_confirmation = table.Column<bool>(type: "boolean", nullable: false),
                    ownership_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    prepared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_tracked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publication_targets", x => new { x.publication_id, x.id });
                    table.ForeignKey(
                        name: "fk_publication_targets_git_connections_git_connection_id",
                        column: x => x.git_connection_id,
                        principalTable: "git_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_publication_targets_publications_publication_id",
                        column: x => x.publication_id,
                        principalTable: "publications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "publication_target_destinations",
                columns: table => new
                {
                    destination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    publication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publication_target_destinations", x => new { x.publication_id, x.target_id, x.destination_id });
                    table.ForeignKey(
                        name: "fk_publication_target_destinations_publication_targets_publica",
                        columns: x => new { x.publication_id, x.target_id },
                        principalTable: "publication_targets",
                        principalColumns: new[] { "publication_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "publication_target_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    publication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repository_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    existing_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publication_target_files", x => new { x.publication_id, x.target_id, x.id });
                    table.ForeignKey(
                        name: "fk_publication_target_files_publication_targets_publication_id",
                        columns: x => new { x.publication_id, x.target_id },
                        principalTable: "publication_targets",
                        principalColumns: new[] { "publication_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_publication_targets_git_connection_id",
                table: "publication_targets",
                column: "git_connection_id");

            migrationBuilder.CreateIndex(
                name: "ix_publications_organization_project_requested_at",
                table: "publications",
                columns: new[] { "organization_id", "project_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_publications_project_state",
                table: "publications",
                columns: new[] { "project_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_publications_revision_id",
                table: "publications",
                column: "revision_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "publication_target_destinations");

            migrationBuilder.DropTable(
                name: "publication_target_files");

            migrationBuilder.DropTable(
                name: "publication_targets");

            migrationBuilder.DropTable(
                name: "publications");
        }
    }
}
