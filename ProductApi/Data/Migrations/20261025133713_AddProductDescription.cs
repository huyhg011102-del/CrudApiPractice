using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductApi.Data.Migrations
{
    // Bài 17: migration thứ hai — thêm một cột mới vào bảng đã có.
    //   Lệnh tạo:  dotnet ef migrations add AddProductDescription
    //   Áp dụng:   dotnet ef database update
    //
    // Mỗi migration có 2 hàm:
    //   Up()   — áp dụng thay đổi (ở đây: ADD COLUMN)
    //   Down() — rollback, đưa DB về trạng thái trước (ở đây: DROP COLUMN)
    //            → EF cảnh báo "may result in the loss of data" vì Down xóa cột kèm dữ liệu trong đó.
    // File .Designer.cs đi kèm lưu "ảnh chụp" model tại thời điểm tạo migration — không sửa tay.
    /// <inheritdoc />
    public partial class AddProductDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);      // cột mới trên bảng đã có dữ liệu → để nullable (hoặc phải có defaultValue)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Products");
        }
    }
}
