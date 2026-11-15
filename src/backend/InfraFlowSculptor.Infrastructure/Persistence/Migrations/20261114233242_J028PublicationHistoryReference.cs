using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class J028PublicationHistoryReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_publication_targets_git_connections_git_connection_id",
                table: "publication_targets");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Historical publication targets outlive deleted Git connections, so restoring this FK can fail after use.
            migrationBuilder.AddForeignKey(
                name: "fk_publication_targets_git_connections_git_connection_id",
                table: "publication_targets",
                column: "git_connection_id",
                principalTable: "git_connections",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
