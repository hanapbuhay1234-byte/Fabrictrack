using FabriTrack.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261024170000_AddProductInventory")]
    public partial class AddProductInventory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "Fabrics",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReorderLevel",
                table: "Fabrics",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 5m);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Fabrics",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                "UPDATE `Fabrics` SET `Sku` = CONCAT('FT-', `FabricId`) WHERE `Sku` IS NULL OR `Sku` = '';");

            migrationBuilder.AlterColumn<string>(
                name: "Sku",
                table: "Fabrics",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fabrics_Sku",
                table: "Fabrics",
                column: "Sku",
                unique: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductSku",
                table: "SaleItems",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "SaleItems",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "SaleItems",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryName",
                table: "SaleItems",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "SaleItems",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE `SaleItems` AS si
                INNER JOIN `Fabrics` AS f ON f.`FabricId` = si.`FabricId`
                LEFT JOIN `Categories` AS c ON c.`CategoryId` = f.`CategoryId`
                SET si.`ProductSku` = f.`Sku`,
                    si.`ProductName` = f.`Name`,
                    si.`Color` = f.`Color`,
                    si.`CategoryName` = COALESCE(c.`Name`, 'Uncategorized'),
                    si.`Unit` = f.`Unit`;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ProductSku",
                table: "SaleItems",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProductName",
                table: "SaleItems",
                type: "varchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CategoryName",
                table: "SaleItems",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "SaleItems",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    InventoryMovementId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FabricId = table.Column<int>(type: "int", nullable: false),
                    SaleId = table.Column<int>(type: "int", nullable: true),
                    MovementType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QuantityChange = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Notes = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecordedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.InventoryMovementId);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Fabrics_FabricId",
                        column: x => x.FabricId,
                        principalTable: "Fabrics",
                        principalColumn: "FabricId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "SaleId",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_FabricId_RecordedAt",
                table: "InventoryMovements",
                columns: new[] { "FabricId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_SaleId",
                table: "InventoryMovements",
                column: "SaleId");

            migrationBuilder.Sql(
                """
                INSERT INTO `InventoryMovements` (`FabricId`, `MovementType`, `QuantityChange`, `Notes`)
                SELECT `FabricId`, 'Opening balance', `StockQuantity`,
                       'Balance when inventory movement tracking was introduced'
                FROM `Fabrics`
                WHERE `StockQuantity` > 0;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_Fabrics_Sku",
                table: "Fabrics");

            migrationBuilder.DropColumn(name: "ProductSku", table: "SaleItems");
            migrationBuilder.DropColumn(name: "ProductName", table: "SaleItems");
            migrationBuilder.DropColumn(name: "Color", table: "SaleItems");
            migrationBuilder.DropColumn(name: "CategoryName", table: "SaleItems");
            migrationBuilder.DropColumn(name: "Unit", table: "SaleItems");
            migrationBuilder.DropColumn(name: "Sku", table: "Fabrics");
            migrationBuilder.DropColumn(name: "ReorderLevel", table: "Fabrics");
            migrationBuilder.DropColumn(name: "IsActive", table: "Fabrics");
        }
    }
}
