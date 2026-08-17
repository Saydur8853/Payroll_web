using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TG.Payroll.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class UsersBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // USERS is a pre-existing legacy table. This migration only records the EF baseline.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The baseline must never drop the legacy USERS table.
        }
    }
}
