using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductApi.Data.Migrations
{
    // Bài 35: tạo stored procedure bằng migration.
    // Tạo migration rỗng (dotnet ef migrations add AddSalesReportProcedure) rồi tự viết SQL bằng migrationBuilder.Sql(...).
    // Nhờ vậy stored procedure cũng được quản lý phiên bản cùng schema, deploy ở đâu cũng có.
    /// <inheritdoc />
    public partial class AddSalesReportProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.sp_GetSalesByCategory
                    @FromDate datetime2
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SELECT c.Name                          AS CategoryName,
                           SUM(oi.Quantity)                AS TotalQuantity,
                           SUM(oi.Quantity * oi.UnitPrice) AS Revenue
                    FROM OrderItems oi
                    JOIN Orders     o ON o.Id = oi.OrderId
                    JOIN Products   p ON p.Id = oi.ProductId
                    JOIN Categories c ON c.Id = p.CategoryId
                    WHERE o.Status = 'Paid'
                      AND o.CreatedAt >= @FromDate
                    GROUP BY c.Name
                    ORDER BY Revenue DESC;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_GetSalesByCategory;");
        }
    }
}
