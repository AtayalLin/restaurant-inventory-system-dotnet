using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Models;

/// <summary>食材：出餐時依配方扣減庫存的對象</summary>
public class Ingredient
{
    public int Id { get; set; }

    [MaxLength(100)]                          // 限制長度，才能建立唯一索引（nvarchar(max) 無法建索引）
    public string Name { get; set; } = "";

    [MaxLength(20)]
    public string Unit { get; set; } = "";    // 單位，例如 g、顆

    [Precision(10, 2)]
    public decimal Stock { get; set; }        // 目前庫存

    [Precision(10, 2)]
    public decimal SafetyStock { get; set; }  // 安全庫存，低於此值需提醒叫貨

    [Precision(10, 2)]
    public decimal MinOrderQty { get; set; }  // 供應商最低訂購量

    // 樂觀鎖：對應 SQL Server 的 rowversion 欄位，每次更新時由資料庫自動變更。
    // EF Core 更新時會自動加上 WHERE RowVersion = 原值；
    // 若資料已被他人修改，影響 0 筆，就會丟出 DbUpdateConcurrencyException。
    // 作用等同 Spring Boot 版的 UPDATE … WHERE version = ?
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;
}