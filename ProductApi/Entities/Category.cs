namespace ProductApi.Entities;

// Bài 15: phía "1" của quan hệ 1-n
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    // Navigation 2 chiều: Category.Products ↔ Product.Category
    public List<Product> Products { get; set; } = new();
}
