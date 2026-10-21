using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Models;

/// <summary>庫存異動紀錄：每次扣庫存或進貨都寫一筆，方便追查</summary>
public class StockLog
{
    public int Id { get; set; }

    public int IngredientId { get; set; }

    [Precision(10, 2)]
    public decimal Change { get; set; }       // 正數＝入庫，負數＝扣庫存

    [MaxLength(200)]
    public string Reason { get; set; } = "";  // 異動原因，例如「出餐：高麗菜炒肉飯 x2」

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // 一律存 UTC，顯示時再轉台灣時間
}