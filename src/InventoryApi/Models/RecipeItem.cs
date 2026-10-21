using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Models;

/// <summary>配方項目：製作一份商品需要多少某種食材</summary>
public class RecipeItem
{
    public int Id { get; set; }

    public int ProductId { get; set; }        // 外鍵：屬於哪個商品

    public int IngredientId { get; set; }     // 外鍵：用到哪個食材
    public Ingredient? Ingredient { get; set; }

    [Precision(10, 2)]
    public decimal Quantity { get; set; }     // 每份用量
}