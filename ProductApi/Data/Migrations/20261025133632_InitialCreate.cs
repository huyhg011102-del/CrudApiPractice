using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ProductApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Stock = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Điện tử" },
                    { 2, "Thời trang" },
                    { 3, "Gia dụng" },
                    { 4, "Sách" },
                    { 5, "Trống" }
                });

            migrationBuilder.InsertData(
                table: "Orders",
                columns: new[] { "Id", "CreatedAt", "CustomerName", "Status", "TotalAmount" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 2, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Nguyễn Văn An", "Paid", 34200000m },
                    { 2, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Trần Thị Bình", "Paid", 750000m },
                    { 3, new DateTime(2026, 4, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Lê Hoàng Cường", "Paid", 2510000m },
                    { 4, new DateTime(2026, 5, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Phạm Thu Dung", "Cancelled", 22000000m },
                    { 5, new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Hoàng Minh Đức", "Pending", 280000m }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CategoryId", "CreatedAt", "CreatedBy", "IsDeleted", "Name", "Price", "Stock", "ViewCount" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 1, 2, 0, 0, 0, 0, DateTimeKind.Utc), "admin", false, "Laptop Dell XPS 13", 32000000m, 5, 0 },
                    { 2, 1, new DateTime(2026, 1, 3, 0, 0, 0, 0, DateTimeKind.Utc), "admin", false, "iPhone 15", 22000000m, 8, 0 },
                    { 3, 1, new DateTime(2026, 1, 4, 0, 0, 0, 0, DateTimeKind.Utc), "staff1", false, "Tai nghe Sony WH-1000XM5", 7500000m, 12, 0 },
                    { 4, 1, new DateTime(2026, 1, 5, 0, 0, 0, 0, DateTimeKind.Utc), "staff1", false, "Chuột Logitech MX", 2200000m, 30, 0 },
                    { 5, 2, new DateTime(2026, 1, 6, 0, 0, 0, 0, DateTimeKind.Utc), "staff2", false, "Áo thun basic", 150000m, 200, 0 },
                    { 6, 2, new DateTime(2026, 1, 7, 0, 0, 0, 0, DateTimeKind.Utc), "staff2", false, "Quần jean", 450000m, 80, 0 },
                    { 7, 2, new DateTime(2026, 1, 8, 0, 0, 0, 0, DateTimeKind.Utc), "staff2", false, "Giày sneaker", 1200000m, 25, 0 },
                    { 8, 3, new DateTime(2026, 1, 9, 0, 0, 0, 0, DateTimeKind.Utc), "staff1", false, "Nồi cơm điện", 1350000m, 15, 0 },
                    { 9, 3, new DateTime(2026, 1, 10, 0, 0, 0, 0, DateTimeKind.Utc), "staff1", false, "Ấm siêu tốc", 320000m, 35, 0 },
                    { 10, 4, new DateTime(2026, 1, 11, 0, 0, 0, 0, DateTimeKind.Utc), "admin", false, "Clean Code", 280000m, 50, 0 },
                    { 11, 4, new DateTime(2026, 1, 12, 0, 0, 0, 0, DateTimeKind.Utc), "admin", false, "Đắc nhân tâm", 95000m, 2, 0 },
                    { 12, 4, new DateTime(2026, 1, 13, 0, 0, 0, 0, DateTimeKind.Utc), "staff2", false, "Atomic Habits", 180000m, 0, 0 }
                });

            migrationBuilder.InsertData(
                table: "OrderItems",
                columns: new[] { "Id", "OrderId", "ProductId", "ProductName", "Quantity", "UnitPrice" },
                values: new object[,]
                {
                    { 1, 1, 1, "Laptop Dell XPS 13", 1, 32000000m },
                    { 2, 1, 4, "Chuột Logitech MX", 1, 2200000m },
                    { 3, 2, 5, "Áo thun basic", 2, 150000m },
                    { 4, 2, 6, "Quần jean", 1, 450000m },
                    { 5, 3, 8, "Nồi cơm điện", 1, 1350000m },
                    { 6, 3, 9, "Ấm siêu tốc", 1, 320000m },
                    { 7, 3, 10, "Clean Code", 3, 280000m },
                    { 8, 4, 2, "iPhone 15", 1, 22000000m },
                    { 9, 5, 10, "Clean Code", 1, 280000m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_CreatedAt",
                table: "Orders",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
