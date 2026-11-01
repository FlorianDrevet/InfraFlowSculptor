using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1707, CA1861

#nullable disable

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_19_ApplicationsBuildAndDeployment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    code_source_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    dockerfile_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    build_context_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    image_repository = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    health_check_path = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    deployment_strategy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    build_registry_environment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.id);
                    table.ForeignKey(
                        name: "fk_applications_components_component_id",
                        column: x => x.component_id,
                        principalTable: "components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_applications_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_steps",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    blocking = table.Column<bool>(type: "boolean", nullable: false),
                    position = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_steps", x => new { x.application_id, x.name });
                    table.ForeignKey(
                        name: "fk_application_steps_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_step_parameters",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_name = table.Column<string>(type: "character varying(64)", nullable: false),
                    value = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_step_parameters", x => new { x.application_id, x.step_name, x.name });
                    table.ForeignKey(
                        name: "fk_application_step_parameters_application_steps_application_i",
                        columns: x => new { x.application_id, x.step_name },
                        principalTable: "application_steps",
                        principalColumns: new[] { "application_id", "name" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_applications_project_id",
                table: "applications",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ux_applications_component_name",
                table: "applications",
                columns: new[] { "component_id", "name_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_applications_resource",
                table: "applications",
                column: "resource_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_step_parameters");

            migrationBuilder.DropTable(
                name: "application_steps");

            migrationBuilder.DropTable(
                name: "applications");
        }
    }
}
