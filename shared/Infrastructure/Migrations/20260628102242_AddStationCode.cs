using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertySearch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStationCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "station_code",
                table: "stations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_stations_station_code",
                table: "stations",
                column: "station_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_stations_station_code",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "station_code",
                table: "stations");
        }
    }
}
