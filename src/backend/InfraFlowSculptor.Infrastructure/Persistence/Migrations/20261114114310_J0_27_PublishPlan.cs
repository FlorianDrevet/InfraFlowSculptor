using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1707, CA1861

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_27_PublishPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE projects SET publishing_preset = 'MonoRepo' WHERE publishing_preset IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "publishing_preset",
                table: "projects",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "MonoRepo",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "publish_default_applications_destination_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "publish_default_infrastructure_destination_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "publish_default_source_code_destination_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "publish_plan_assignments",
                columns: table => new
                {
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    part = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publish_plan_assignments", x => new { x.project_id, x.component_id, x.part });
                    table.ForeignKey(
                        name: "fk_publish_plan_assignments_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "publish_plan_destinations",
                columns: table => new
                {
                    destination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    git_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_repository_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    base_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publish_plan_destinations", x => new { x.project_id, x.destination_id });
                    table.ForeignKey(
                        name: "fk_publish_plan_destinations_git_connections_git_connection_id",
                        column: x => x.git_connection_id,
                        principalTable: "git_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_publish_plan_destinations_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_publish_plan_destinations_git_connection_id",
                table: "publish_plan_destinations",
                column: "git_connection_id");

            migrationBuilder.CreateIndex(
                name: "ux_publish_plan_destinations_repository_path",
                table: "publish_plan_destinations",
                columns: new[] { "project_id", "git_connection_id", "provider_repository_id", "base_path" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "publish_plan_assignments");

            migrationBuilder.DropTable(
                name: "publish_plan_destinations");

            migrationBuilder.DropColumn(
                name: "publish_default_applications_destination_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "publish_default_infrastructure_destination_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "publish_default_source_code_destination_id",
                table: "projects");

            migrationBuilder.AlterColumn<string>(
                name: "publishing_preset",
                table: "projects",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "MonoRepo");
        }
    }
}
