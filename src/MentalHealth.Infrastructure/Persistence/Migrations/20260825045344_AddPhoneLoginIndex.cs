using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentalHealth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneLoginIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "PhoneNumberIndex",
                table: "AspNetUsers",
                column: "PhoneNumber",
                unique: true,
                filter: "\"PhoneNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "PhoneNumberIndex",
                table: "AspNetUsers");
        }
    }
}
