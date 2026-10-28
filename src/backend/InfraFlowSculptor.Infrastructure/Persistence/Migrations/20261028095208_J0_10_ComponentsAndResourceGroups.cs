using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1707, CA1861

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_10_ComponentsAndResourceGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "components",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    code_key = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    targets_all_environments = table.Column<bool>(type: "boolean", nullable: false),
                    target_environment_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false, defaultValueSql: "ARRAY[]::uuid[]"),
                    removed_resource_behavior = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    deletion_protection_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    first_published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_components", x => x.id);
                    table.ForeignKey(
                        name: "fk_components_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "component_single_targets",
                columns: table => new
                {
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    region = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    deployment_connection = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_protected = table.Column<bool>(type: "boolean", nullable: false),
                    approvers = table.Column<string[]>(type: "text[]", nullable: false),
                    runners = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_component_single_targets", x => x.component_id);
                    table.ForeignKey(
                        name: "fk_component_single_targets_components_component_id",
                        column: x => x.component_id,
                        principalTable: "components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "component_tags",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_component_tags", x => new { x.component_id, x.key });
                    table.ForeignKey(
                        name: "fk_component_tags_components_component_id",
                        column: x => x.component_id,
                        principalTable: "components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    region = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_groups", x => new { x.component_id, x.id });
                    table.ForeignKey(
                        name: "fk_resource_groups_components_component_id",
                        column: x => x.component_id,
                        principalTable: "components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_group_name_overrides",
                columns: table => new
                {
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(90)", maxLength: 90, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_group_name_overrides", x => new { x.component_id, x.resource_group_id, x.environment_id });
                    table.ForeignKey(
                        name: "fk_resource_group_name_overrides_resource_groups_component_id_",
                        columns: x => new { x.component_id, x.resource_group_id },
                        principalTable: "resource_groups",
                        principalColumns: new[] { "component_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_group_tags",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_group_tags", x => new { x.component_id, x.resource_group_id, x.key });
                    table.ForeignKey(
                        name: "fk_resource_group_tags_resource_groups_component_id_resource_g",
                        columns: x => new { x.component_id, x.resource_group_id },
                        principalTable: "resource_groups",
                        principalColumns: new[] { "component_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_components_project_code",
                table: "components",
                columns: new[] { "project_id", "code_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_components_project_name",
                table: "components",
                columns: new[] { "project_id", "name_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_resource_groups_component_name",
                table: "resource_groups",
                columns: new[] { "component_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "component_single_targets");

            migrationBuilder.DropTable(
                name: "component_tags");

            migrationBuilder.DropTable(
                name: "resource_group_name_overrides");

            migrationBuilder.DropTable(
                name: "resource_group_tags");

            migrationBuilder.DropTable(
                name: "resource_groups");

            migrationBuilder.DropTable(
                name: "components");
        }
    }
}
