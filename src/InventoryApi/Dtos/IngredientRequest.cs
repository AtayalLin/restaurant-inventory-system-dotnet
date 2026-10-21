using System.ComponentModel.DataAnnotations;
using InventoryApi.Models;

namespace InventoryApi.Dtos;

/// <summary>新增／修改食材時前端送來的資料；只接收允許修改的欄位</summary>
public class IngredientRequest
{
    [Required(ErrorMessage = "請輸入食材名稱"), MaxLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "請輸入單位"), MaxLength(20)]
    public string Unit { get; set; } = "";

    [Range(typeof(decimal), "0", "99999999", ErrorMessage = "庫存不可為負數")]
    public decimal Stock { get; set; }

    [Range(typeof(decimal), "0", "99999999", ErrorMessage = "安全庫存不可為負數")]
    public decimal SafetyStock { get; set; }

    [Range(typeof(decimal), "0", "99999999", ErrorMessage = "最低訂購量不可為負數")]
    public decimal MinOrderQty { get; set; }

    // 修改時必填：前端讀取資料時拿到的版本號，送回來給樂觀鎖比對
    public byte[]? RowVersion { get; set; }

    /// <summary>把請求內容寫進 Entity（新增與修改共用）</summary>
    public void ApplyTo(Ingredient entity)
    {
        entity.Name = Name.Trim();
        entity.Unit = Unit.Trim();
        entity.Stock = Stock;
        entity.SafetyStock = SafetyStock;
        entity.MinOrderQty = MinOrderQty;
    }
}