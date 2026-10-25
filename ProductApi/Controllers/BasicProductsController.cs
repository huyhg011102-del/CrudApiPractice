using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductApi.Data;
using ProductApi.Dtos;
using ProductApi.Entities;

namespace ProductApi.Controllers;

// ======================================================================================
//  Bài 3 → 10: CRUD "thuần" dùng DbContext trực tiếp trong controller.
//  ĐÂY LÀ PHẦN HAY PHẢI LIVECODE NHẤT — nên gõ lại được trong ~15 phút không cần nhìn.
//  (Bản "chuẩn" nhiều tầng từ Bài 11 trở đi nằm ở ProductsController.)
// ======================================================================================

// Bài 3: kế thừa ControllerBase (API không cần View), gắn [ApiController] và [Route].
// [ApiController] bật: tự trả 400 khi model không hợp lệ (Bài 10), tự suy luận [FromBody]/[FromRoute]...
[ApiController]
[Route("api/basic/products")]
public class BasicProductsController(AppDbContext db) : ControllerBase    // Bài 2: DbContext được inject qua DI
{
    // Bài 3 + 8 + 9: GET danh sách — async, trả DTO.
    // Trước Bài 9 (chưa có DTO) sẽ là: return await db.Products.ToListAsync();
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll(CancellationToken ct)
    {
        var products = await db.Products
            .AsNoTracking()                                   // Bài 16: chỉ đọc
            .OrderBy(p => p.Id)
            .Select(p => new ProductDto(p.Id, p.Name, p.Price))
            .ToListAsync(ct);                                 // Bài 8: ToListAsync, KHÔNG .Result

        return Ok(products);
    }

    // Bài 4: GET theo id, 404 nếu không có.
    // FindAsync tìm trong change tracker trước, có rồi thì khỏi query DB.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([id], ct);
        if (product is null)
            return NotFound();

        return product.ToDto();          // ActionResult<T> tự bọc thành 200 OK
    }

    // Bài 5: POST tạo mới → 201 Created + header Location: /api/basic/products/{id}
    // Bài 10: CreateProductRequest có [Required], [MaxLength], [Range] → sai thì tự trả 400.
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var product = new Product
        {
            Name = request.Name,
            Price = request.Price,
            Stock = request.Stock,
            CategoryId = request.CategoryId,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "anonymous",
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);      // sau dòng này product.Id mới có giá trị

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product.ToDto());
    }

    // Bài 6: PUT cập nhật → 404 nếu không có, 204 nếu thành công.
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateProductRequest request, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([id], ct);
        if (product is null)
            return NotFound();

        // Entity đang được track → chỉ cần gán, SaveChanges sẽ sinh UPDATE cho đúng các cột thay đổi.
        // Cách khác: db.Entry(product).CurrentValues.SetValues(request);
        product.Name = request.Name;
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.CategoryId = request.CategoryId;
        product.Description = request.Description;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // Bài 7: DELETE → 204 khi xóa được, 404 khi không có.
    // .NET 7+: ExecuteDeleteAsync xóa thẳng bằng 1 câu DELETE, không cần load entity.
    // (Đây là xóa THẬT. Bản xóa mềm — Bài 18 — nằm ở ProductsController.)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : NoContent();

        // Cách cổ điển (2 round-trip):
        //   var product = await db.Products.FindAsync([id], ct);
        //   if (product is null) return NotFound();
        //   db.Products.Remove(product);
        //   await db.SaveChangesAsync(ct);
        //   return NoContent();
        //
        // Lưu ý: sản phẩm đã có trong OrderItems sẽ không xóa được (khóa ngoại Restrict) → middleware trả 409.
    }
}
