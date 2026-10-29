using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1707, CA1861

#nullable disable

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_16_ApplicationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "default_identity_resource_id",
                table: "resources",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    source_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    output_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    key_vault_resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    secret_name = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: true),
                    secret_name_key = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: true),
                    secret_feed_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    pipeline_variable_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    pipeline_variable_name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    identity_resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    secret_value_declared_non_secret = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_app_settings_components_component_id",
                        column: x => x.component_id,
                        principalTable: "components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_app_settings_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "app_setting_literals",
                columns: table => new
                {
                    app_setting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_setting_literals", x => x.app_setting_id);
                    table.ForeignKey(
                        name: "fk_app_setting_literals_app_settings_app_setting_id",
                        column: x => x.app_setting_id,
                        principalTable: "app_settings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "app_setting_literal_overrides",
                columns: table => new
                {
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_setting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_setting_literal_overrides", x => new { x.app_setting_id, x.target_id });
                    table.ForeignKey(
                        name: "fk_app_setting_literal_overrides_app_setting_literals_app_sett",
                        column: x => x.app_setting_id,
                        principalTable: "app_setting_literals",
                        principalColumn: "app_setting_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_settings_component_id",
                table: "app_settings",
                column: "component_id");

            migrationBuilder.CreateIndex(
                name: "ux_app_settings_application_name",
                table: "app_settings",
                columns: new[] { "application_resource_id", "name_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_app_settings_pipeline_secret_writer",
                table: "app_settings",
                columns: new[] { "key_vault_resource_id", "secret_name_key" },
                unique: true,
                filter: "\"secret_feed_kind\" = 'PipelineSecret'");

            migrationBuilder.CreateIndex(
                name: "ux_app_settings_project_pipeline_variable",
                table: "app_settings",
                columns: new[] { "project_id", "pipeline_variable_name_key" },
                unique: true,
                filter: "\"pipeline_variable_name_key\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_setting_literal_overrides");

            migrationBuilder.DropTable(
                name: "app_setting_literals");

            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropColumn(
                name: "default_identity_resource_id",
                table: "resources");
        }
    }
}
