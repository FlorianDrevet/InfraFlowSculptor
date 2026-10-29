using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1707, CA1861

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J0_20_FindingAcknowledgmentDetailKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "detail_key",
                table: "finding_acknowledgments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_finding_acknowledgments_identity",
                table: "finding_acknowledgments",
                columns: new[] { "project_id", "finding_code", "subject_type", "subject_id", "environment_id", "object_version", "detail_key" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_finding_acknowledgments_identity",
                table: "finding_acknowledgments");

            migrationBuilder.DropColumn(
                name: "detail_key",
                table: "finding_acknowledgments");
        }
    }
}
