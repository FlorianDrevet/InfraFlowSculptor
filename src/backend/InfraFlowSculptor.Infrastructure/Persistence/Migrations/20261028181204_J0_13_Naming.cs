using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1707

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_13_Naming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "default_name_template",
                table: "components",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "component_resource_type_naming_overrides",
                columns: table => new
                {
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    abbreviation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_component_resource_type_naming_overrides", x => new { x.component_id, x.type });
                    table.ForeignKey(
                        name: "fk_component_resource_type_naming_overrides_components_compone",
                        column: x => x.component_id,
                        principalTable: "components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_resource_type_naming_overrides",
                columns: table => new
                {
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    abbreviation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_resource_type_naming_overrides", x => new { x.project_id, x.type });
                    table.ForeignKey(
                        name: "fk_project_resource_type_naming_overrides_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "component_resource_type_naming_overrides");

            migrationBuilder.DropTable(
                name: "project_resource_type_naming_overrides");

            migrationBuilder.DropColumn(
                name: "default_name_template",
                table: "components");
        }
    }
}
