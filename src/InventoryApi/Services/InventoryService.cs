using InventoryApi.Data;
using InventoryApi.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Services;

/// <summary>出餐結果的狀態</summary>
public enum ServeStatus { Success, ProductNotFound, InsufficientStock, Conflict }

/// <summary>庫存不足的明細：哪個食材、需要多少、目前有多少</summary>
public record Shortage(int IngredientId, string Name, decimal Required, decimal Available);

/// <summary>出餐結果；庫存不足時附上所有不足項目</summary>
public record ServeResult(ServeStatus Status, List<Shortage>? Shortages = null);

/// <summary>庫存相關的業務邏輯（對應 Spring Boot 版的 InventoryService）</summary>
public class InventoryService(AppDbContext db)
{
    private const int MaxRetries = 3;   // 樂觀鎖衝突時最多重試次數

    /// <summary>出餐：依配方扣除所有食材；任一不足則整筆不扣</summary>
    public async Task<ServeResult> ServeAsync(int productId, int quantity)
    {
        for (var attempt = 1; ; attempt++)
        {
            // 一次載入商品、配方與對應的食材（Include 相當於 JPA 的 JOIN FETCH）
            var product = await db.Products
                .Include(p => p.Recipe)
                .ThenInclude(r => r.Ingredient)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product is null)
                return new ServeResult(ServeStatus.ProductNotFound);

            // 先檢查全部食材，收集「所有」不足項目，而不是遇到第一個就停
            var shortages = product.Recipe
                .Where(r => r.Ingredient!.Stock < r.Quantity * quantity)
                .Select(r => new Shortage(
                    r.IngredientId, r.Ingredient!.Name, r.Quantity * quantity, r.Ingredient!.Stock))
                .ToList();

            if (shortages.Count > 0)
                return new ServeResult(ServeStatus.InsufficientStock, shortages);

            // 全部足夠才開始扣，並為每一項寫一筆庫存異動紀錄
            foreach (var item in product.Recipe)
            {
                var used = item.Quantity * quantity;
                item.Ingredient!.Stock -= used;
                db.StockLogs.Add(new StockLog
                {
                    IngredientId = item.IngredientId,
                    Change = -used,
                    Reason = $"出餐：{product.Name} x{quantity}"
                });
            }

            try
            {
                // 單次 SaveChanges 會包在同一個資料庫交易裡：全部成功，或全部回滾。
                // 作用等同 Spring Boot 的 @Transactional
                await db.SaveChangesAsync();
                return new ServeResult(ServeStatus.Success);
            }
            catch (DbUpdateConcurrencyException)
            {
                // 讀取之後有人改了其中某個食材的庫存（RowVersion 不符），交易已整筆回滾
                if (attempt >= MaxRetries)
                    return new ServeResult(ServeStatus.Conflict);

                // 清掉這次讀到的舊資料，下一輪重新讀取最新庫存再判斷一次
                db.ChangeTracker.Clear();
            }
        }
    }
}