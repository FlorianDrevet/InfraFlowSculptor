using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1707, CA1861

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_24_Revisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    author_tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    model_version = table.Column<int>(type: "integer", nullable: false),
                    catalog_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    infrastructure_language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ci_platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    input_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    deployment_plan_json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_revisions_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "revision_changes",
                columns: table => new
                {
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subject = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    description_fr = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    description_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revision_changes", x => new { x.revision_id, x.position });
                    table.ForeignKey(
                        name: "fk_revision_changes_revisions_revision_id",
                        column: x => x.revision_id,
                        principalTable: "revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "revision_files",
                columns: table => new
                {
                    relative_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revision_files", x => new { x.revision_id, x.relative_path });
                    table.ForeignKey(
                        name: "fk_revision_files_revisions_revision_id",
                        column: x => x.revision_id,
                        principalTable: "revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "revision_findings",
                columns: table => new
                {
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    message_fr = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    message_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revision_findings", x => new { x.revision_id, x.id });
                    table.ForeignKey(
                        name: "fk_revision_findings_revisions_revision_id",
                        column: x => x.revision_id,
                        principalTable: "revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "revision_inputs",
                columns: table => new
                {
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_version = table.Column<int>(type: "integer", nullable: false),
                    catalog_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    infrastructure_language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ci_platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    snapshot = table.Column<string>(type: "text", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revision_inputs", x => x.revision_id);
                    table.ForeignKey(
                        name: "fk_revision_inputs_revisions_revision_id",
                        column: x => x.revision_id,
                        principalTable: "revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_revision_files_sha256",
                table: "revision_files",
                column: "sha256");

            migrationBuilder.CreateIndex(
                name: "ix_revisions_organization_state_created_at",
                table: "revisions",
                columns: new[] { "organization_id", "state", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_revisions_project_input_number",
                table: "revisions",
                columns: new[] { "project_id", "input_sha256", "number" });

            migrationBuilder.CreateIndex(
                name: "ux_revisions_project_number",
                table: "revisions",
                columns: new[] { "project_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "revision_changes");

            migrationBuilder.DropTable(
                name: "revision_files");

            migrationBuilder.DropTable(
                name: "revision_findings");

            migrationBuilder.DropTable(
                name: "revision_inputs");

            migrationBuilder.DropTable(
                name: "revisions");
        }
    }
}
