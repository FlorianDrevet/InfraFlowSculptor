using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [SuppressMessage("Naming", "CA1707", Justification = "The migration identifier is fixed by the J0-02 plan.")]
    public partial class J0_02_OrganizationAccessRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "allowed_email_domains",
                table: "organizations",
                type: "text[]",
                nullable: false,
                defaultValue: Array.Empty<string>());

            migrationBuilder.AddColumn<Guid[]>(
                name: "allowed_tenant_ids",
                table: "organizations",
                type: "uuid[]",
                nullable: false,
                defaultValue: Array.Empty<Guid>());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allowed_email_domains",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "allowed_tenant_ids",
                table: "organizations");
        }
    }
}
