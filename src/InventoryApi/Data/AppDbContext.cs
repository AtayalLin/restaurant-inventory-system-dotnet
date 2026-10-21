using InventoryApi.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Data;

/// <summary>資料庫入口：每個 DbSet 對應一張資料表</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();
    public DbSet<StockLog> StockLogs => Set<StockLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 食材名稱不可重複
        modelBuilder.Entity<Ingredient>().HasIndex(i => i.Name).IsUnique();

        // 同一個商品的配方裡，同一種食材只能出現一次
        modelBuilder.Entity<RecipeItem>()
            .HasIndex(r => new { r.ProductId, r.IngredientId }).IsUnique();

        // 種子資料：建表時一併寫入，方便直接測試 API
        modelBuilder.Entity<Ingredient>().HasData(
            new Ingredient { Id = 1, Name = "高麗菜", Unit = "g", Stock = 5000, SafetyStock = 1000, MinOrderQty = 3000 },
            new Ingredient { Id = 2, Name = "豬肉片", Unit = "g", Stock = 3000, SafetyStock = 800,  MinOrderQty = 2000 },
            new Ingredient { Id = 3, Name = "白飯",   Unit = "g", Stock = 8000, SafetyStock = 2000, MinOrderQty = 5000 });

        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "高麗菜炒肉飯", Price = 120 });

        modelBuilder.Entity<RecipeItem>().HasData(
            new RecipeItem { Id = 1, ProductId = 1, IngredientId = 1, Quantity = 150 },
            new RecipeItem { Id = 2, ProductId = 1, IngredientId = 2, Quantity = 100 },
            new RecipeItem { Id = 3, ProductId = 1, IngredientId = 3, Quantity = 250 });
    }
}