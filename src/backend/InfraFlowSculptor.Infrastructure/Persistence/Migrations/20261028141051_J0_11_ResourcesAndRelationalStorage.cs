using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1707, CA1861

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_11_ResourcesAndRelationalStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "response",
                table: "user_idempotency_keys",
                type: "text",
                nullable: true,
                oldClrType: typeof(JsonElement),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "content_json",
                table: "project_wizard_drafts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "payload",
                table: "outbox_messages",
                type: "text",
                nullable: false,
                oldClrType: typeof(JsonElement),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "response",
                table: "idempotency_keys",
                type: "text",
                nullable: true,
                oldClrType: typeof(JsonElement),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                table: "component_single_targets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE component_single_targets SET id = gen_random_uuid() WHERE id = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.AlterColumn<string>(
                name: "diff",
                table: "audit_events",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.CreateTable(
                name: "resources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    logical_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_existing = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resources", x => new { x.component_id, x.id });
                    table.ForeignKey(
                        name: "fk_resources_components_component_id",
                        column: x => x.component_id,
                        principalTable: "components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "existing_resource_ids",
                columns: table => new
                {
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_existing_resource_ids", x => new { x.component_id, x.resource_id, x.target_id });
                    table.ForeignKey(
                        name: "fk_existing_resource_ids_resources_component_id_resource_id",
                        columns: x => new { x.component_id, x.resource_id },
                        principalTable: "resources",
                        principalColumns: new[] { "component_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_children",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(90)", maxLength: 90, nullable: false),
                    name_key = table.Column<string>(type: "character varying(90)", maxLength: 90, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_children", x => new { x.component_id, x.resource_id, x.id });
                    table.ForeignKey(
                        name: "fk_resource_children_resources_component_id_resource_id",
                        columns: x => new { x.component_id, x.resource_id },
                        principalTable: "resources",
                        principalColumns: new[] { "component_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_name_overrides",
                columns: table => new
                {
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(90)", maxLength: 90, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_name_overrides", x => new { x.component_id, x.resource_id, x.target_id });
                    table.ForeignKey(
                        name: "fk_resource_name_overrides_resources_component_id_resource_id",
                        columns: x => new { x.component_id, x.resource_id },
                        principalTable: "resources",
                        principalColumns: new[] { "component_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_presences",
                columns: table => new
                {
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_present = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_presences", x => new { x.component_id, x.resource_id, x.target_id });
                    table.ForeignKey(
                        name: "fk_resource_presences_resources_component_id_resource_id",
                        columns: x => new { x.component_id, x.resource_id },
                        principalTable: "resources",
                        principalColumns: new[] { "component_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_tags",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_tags", x => new { x.component_id, x.resource_id, x.key });
                    table.ForeignKey(
                        name: "fk_resource_tags_resources_component_id_resource_id",
                        columns: x => new { x.component_id, x.resource_id },
                        principalTable: "resources",
                        principalColumns: new[] { "component_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_value_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    property_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    member_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    position = table.Column<int>(type: "integer", nullable: true),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    string_value = table.Column<string>(type: "text", nullable: true),
                    boolean_value = table.Column<bool>(type: "boolean", nullable: true),
                    integer_value = table.Column<long>(type: "bigint", nullable: true),
                    number_value = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_value_nodes", x => new { x.component_id, x.resource_id, x.id });
                    table.CheckConstraint("ck_resource_value_nodes_shape", "(parent_id IS NULL AND property_name IS NOT NULL AND member_name IS NULL AND position IS NULL) OR (parent_id IS NOT NULL AND property_name IS NULL AND target_id IS NULL AND ((member_name IS NOT NULL AND position IS NULL) OR (member_name IS NULL AND position IS NOT NULL AND position >= 0)))");
                    table.CheckConstraint("ck_resource_value_nodes_typed_value", "(kind = 'Absent' AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NULL) OR (kind = 'Textual' AND string_value IS NOT NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NULL) OR (kind = 'Truth' AND string_value IS NULL AND boolean_value IS NOT NULL AND integer_value IS NULL AND number_value IS NULL) OR (kind = 'WholeNumber' AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NOT NULL AND number_value IS NULL) OR (kind = 'DecimalNumber' AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NOT NULL) OR (kind IN ('Sequence', 'Map') AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NULL)");
                    table.ForeignKey(
                        name: "fk_resource_value_nodes_resources_component_id_resource_id",
                        columns: x => new { x.component_id, x.resource_id },
                        principalTable: "resources",
                        principalColumns: new[] { "component_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_child_value_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    property_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    member_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    position = table.Column<int>(type: "integer", nullable: true),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    string_value = table.Column<string>(type: "text", nullable: true),
                    boolean_value = table.Column<bool>(type: "boolean", nullable: true),
                    integer_value = table.Column<long>(type: "bigint", nullable: true),
                    number_value = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_child_value_nodes", x => new { x.component_id, x.resource_id, x.resource_child_id, x.id });
                    table.CheckConstraint("ck_resource_child_value_nodes_shape", "parent_id IS NULL AND property_name IS NOT NULL AND member_name IS NULL AND position IS NULL AND target_id IS NULL OR parent_id IS NOT NULL AND property_name IS NULL AND target_id IS NULL AND ((member_name IS NOT NULL AND position IS NULL) OR (member_name IS NULL AND position IS NOT NULL AND position >= 0))");
                    table.CheckConstraint("ck_resource_child_value_nodes_typed_value", "(kind = 'Absent' AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NULL) OR (kind = 'Textual' AND string_value IS NOT NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NULL) OR (kind = 'Truth' AND string_value IS NULL AND boolean_value IS NOT NULL AND integer_value IS NULL AND number_value IS NULL) OR (kind = 'WholeNumber' AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NOT NULL AND number_value IS NULL) OR (kind = 'DecimalNumber' AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NOT NULL) OR (kind IN ('Sequence', 'Map') AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NULL)");
                    table.ForeignKey(
                        name: "fk_resource_child_value_nodes_resource_children_component_id_r",
                        columns: x => new { x.component_id, x.resource_id, x.resource_child_id },
                        principalTable: "resource_children",
                        principalColumns: new[] { "component_id", "resource_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_component_single_targets_id",
                table: "component_single_targets",
                column: "id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_resource_child_value_nodes_parent_member",
                table: "resource_child_value_nodes",
                columns: new[] { "component_id", "resource_id", "resource_child_id", "parent_id", "member_name" },
                unique: true,
                filter: "member_name IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_resource_child_value_nodes_parent_position",
                table: "resource_child_value_nodes",
                columns: new[] { "component_id", "resource_id", "resource_child_id", "parent_id", "position" },
                unique: true,
                filter: "position IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_resource_child_value_nodes_property",
                table: "resource_child_value_nodes",
                columns: new[] { "component_id", "resource_id", "resource_child_id", "property_name" },
                unique: true,
                filter: "parent_id IS NULL AND property_name IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_resource_children_name",
                table: "resource_children",
                columns: new[] { "component_id", "resource_id", "name_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_resource_value_nodes_base_property",
                table: "resource_value_nodes",
                columns: new[] { "component_id", "resource_id", "property_name" },
                unique: true,
                filter: "parent_id IS NULL AND target_id IS NULL AND property_name IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_resource_value_nodes_parent_member",
                table: "resource_value_nodes",
                columns: new[] { "component_id", "resource_id", "parent_id", "member_name" },
                unique: true,
                filter: "member_name IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_resource_value_nodes_parent_position",
                table: "resource_value_nodes",
                columns: new[] { "component_id", "resource_id", "parent_id", "position" },
                unique: true,
                filter: "position IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_resource_value_nodes_target_property",
                table: "resource_value_nodes",
                columns: new[] { "component_id", "resource_id", "target_id", "property_name" },
                unique: true,
                filter: "parent_id IS NULL AND target_id IS NOT NULL AND property_name IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_resources_component_type_logical_name",
                table: "resources",
                columns: new[] { "component_id", "type", "logical_name" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE resources
                    ADD CONSTRAINT fk_resources_resource_groups
                    FOREIGN KEY (component_id, resource_group_id)
                    REFERENCES resource_groups (component_id, id)
                    ON DELETE RESTRICT;

                ALTER TABLE resource_value_nodes
                    ADD CONSTRAINT fk_resource_value_nodes_parent
                    FOREIGN KEY (component_id, resource_id, parent_id)
                    REFERENCES resource_value_nodes (component_id, resource_id, id)
                    ON DELETE CASCADE
                    DEFERRABLE INITIALLY DEFERRED;

                ALTER TABLE resource_child_value_nodes
                    ADD CONSTRAINT fk_resource_child_value_nodes_parent
                    FOREIGN KEY (component_id, resource_id, resource_child_id, parent_id)
                    REFERENCES resource_child_value_nodes (component_id, resource_id, resource_child_id, id)
                    ON DELETE CASCADE
                    DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("J0-11 cannot be rolled back because that would restore PostgreSQL JSONB columns.");
    }
}
