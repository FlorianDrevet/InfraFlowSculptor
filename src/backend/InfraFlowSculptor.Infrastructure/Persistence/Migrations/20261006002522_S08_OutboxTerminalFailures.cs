using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [SuppressMessage("Naming", "CA1707", Justification = "The migration identifier is fixed by the S-08 plan.")]
    public partial class S08_OutboxTerminalFailures : Migration
    {
        private static readonly string[] OutboxRetryIndexColumns = ["sent_at", "next_attempt_at", "created_at"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_sent_at_next_attempt_at_created_at",
                table: "outbox_messages");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "failed_at",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_sent_at_next_attempt_at_created_at",
                table: "outbox_messages",
                columns: OutboxRetryIndexColumns,
                filter: "sent_at IS NULL AND failed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_sent_at_next_attempt_at_created_at",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "failed_at",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_sent_at_next_attempt_at_created_at",
                table: "outbox_messages",
                columns: OutboxRetryIndexColumns,
                filter: "sent_at IS NULL");
        }
    }
}
