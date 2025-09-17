using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RatedAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "Appointments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "Appointments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RatedAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "Appointments");
        }
    }
}
