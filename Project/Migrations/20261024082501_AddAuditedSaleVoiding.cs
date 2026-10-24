using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditedSaleVoiding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVoided",
                table: "Sales",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Sales",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "Sales",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidedByUserId",
                table: "Sales",
                type: "varchar(450)",
                maxLength: 450,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVoided",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                table: "Sales");
        }
    }
}
