using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Models;

/// <summary>商品：例如「高麗菜炒肉飯」，透過配方對應到多種食材</summary>
public class Product
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = "";

    [Precision(10, 2)]
    public decimal Price { get; set; }

    // 一對多：一個商品有多筆配方項目（EF Core 依 RecipeItem.ProductId 自動建立外鍵）
    public List<RecipeItem> Recipe { get; set; } = [];
}