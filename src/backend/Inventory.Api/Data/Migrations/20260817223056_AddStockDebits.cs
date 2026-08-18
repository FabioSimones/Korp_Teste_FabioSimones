using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventory.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockDebits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_debit_operations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_debit_operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stock_debit_operation_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockDebitOperationId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    BalanceAfter = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_debit_operation_items", x => x.Id);
                    table.CheckConstraint("CK_stock_debit_operation_items_quantity_positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_stock_debit_operation_items_stock_debit_operations_StockDeb~",
                        column: x => x.StockDebitOperationId,
                        principalTable: "stock_debit_operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_balance_non_negative",
                table: "products",
                sql: "\"Balance\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_stock_debit_operation_items_StockDebitOperationId",
                table: "stock_debit_operation_items",
                column: "StockDebitOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_debit_operations_OperationId",
                table: "stock_debit_operations",
                column: "OperationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_debit_operation_items");

            migrationBuilder.DropTable(
                name: "stock_debit_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_balance_non_negative",
                table: "products");
        }
    }
}
