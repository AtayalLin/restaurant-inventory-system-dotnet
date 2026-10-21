using InventoryApi.Data;
using InventoryApi.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InventoryApi.Tests;

/// <summary>
/// 出餐扣庫存的整合測試，連到真正的 SQL Server（測試資料庫）。
/// 種子資料：商品 1「高麗菜炒肉飯」每份用 高麗菜(Id=1) 150g、豬肉片(Id=2) 100g、白飯(Id=3) 250g
/// </summary>
public class InventoryServiceTests(DatabaseFixture fx) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task 庫存足夠_扣除所有食材並寫入異動紀錄()
    {
        await ResetAsync(cabbage: 5000, pork: 3000, rice: 8000);

        await using var db = fx.CreateContext();
        var result = await new InventoryService(db).ServeAsync(productId: 1, quantity: 2);

        Assert.Equal(ServeStatus.Success, result.Status);

        var stocks = await GetStocksAsync();
        Assert.Equal(4700m, stocks[1]);   // 5000 - 150×2
        Assert.Equal(2800m, stocks[2]);   // 3000 - 100×2
        Assert.Equal(7500m, stocks[3]);   // 8000 - 250×2
        Assert.Equal(3, await CountLogsAsync());
    }

    [Fact]
    public async Task 兩種食材不足_列出所有不足項目且完全不扣()
    {
        await ResetAsync(cabbage: 100, pork: 50, rice: 8000);   // 高麗菜、豬肉片不夠出 2 份

        await using var db = fx.CreateContext();
        var result = await new InventoryService(db).ServeAsync(productId: 1, quantity: 2);

        Assert.Equal(ServeStatus.InsufficientStock, result.Status);
        Assert.Equal(2, result.Shortages!.Count);
        Assert.Contains(result.Shortages, s => s.Name == "高麗菜" && s.Required == 300m && s.Available == 100m);
        Assert.Contains(result.Shortages, s => s.Name == "豬肉片" && s.Required == 200m && s.Available == 50m);

        // 整筆拒絕：白飯雖然夠，也不能被扣
        var stocks = await GetStocksAsync();
        Assert.Equal(100m, stocks[1]);
        Assert.Equal(50m, stocks[2]);
        Assert.Equal(8000m, stocks[3]);
        Assert.Equal(0, await CountLogsAsync());
    }

    [Fact]
    public async Task 商品不存在_回傳ProductNotFound()
    {
        await using var db = fx.CreateContext();
        var result = await new InventoryService(db).ServeAsync(productId: 999, quantity: 1);

        Assert.Equal(ServeStatus.ProductNotFound, result.Status);
    }

    [Fact]
    public async Task 兩個請求同時出餐_庫存足夠_兩筆都成功且庫存正確()
    {
        await ResetAsync(cabbage: 5000, pork: 3000, rice: 8000);

        // 兩個獨立的 DbContext 同時執行，模擬兩位店員同時按下出餐
        var results = await Task.WhenAll(ServeWithNewContextAsync(1), ServeWithNewContextAsync(1));

        Assert.All(results, r => Assert.Equal(ServeStatus.Success, r.Status));

        // 若沒有樂觀鎖，兩筆可能都讀到 5000 再各自寫回 4850，造成少扣一份
        var stocks = await GetStocksAsync();
        Assert.Equal(4700m, stocks[1]);
        Assert.Equal(2800m, stocks[2]);
        Assert.Equal(7500m, stocks[3]);
        Assert.Equal(6, await CountLogsAsync());
    }

    [Fact]
    public async Task 兩個請求同時出餐_庫存只夠一份_只有一筆成功且不會變成負數()
    {
        await ResetAsync(cabbage: 200, pork: 3000, rice: 8000);   // 高麗菜只夠一份（150g）

        var results = await Task.WhenAll(ServeWithNewContextAsync(1), ServeWithNewContextAsync(1));

        Assert.Single(results, r => r.Status == ServeStatus.Success);
        Assert.Single(results, r => r.Status == ServeStatus.InsufficientStock);

        var stocks = await GetStocksAsync();
        Assert.Equal(50m, stocks[1]);     // 200 - 150，只扣一次
    }

    // ---------- 測試輔助方法 ----------

    /// <summary>用全新的 DbContext 出餐，代表一個獨立的請求</summary>
    private async Task<ServeResult> ServeWithNewContextAsync(int quantity)
    {
        await using var db = fx.CreateContext();
        return await new InventoryService(db).ServeAsync(productId: 1, quantity);
    }

    /// <summary>重設三種食材的庫存，並清空異動紀錄，讓每個測試從相同狀態開始</summary>
    private async Task ResetAsync(decimal cabbage, decimal pork, decimal rice)
    {
        await using var db = fx.CreateContext();
        await db.StockLogs.ExecuteDeleteAsync();
        await SetStockAsync(db, 1, cabbage);
        await SetStockAsync(db, 2, pork);
        await SetStockAsync(db, 3, rice);
    }

    private static Task<int> SetStockAsync(AppDbContext db, int id, decimal stock) =>
        db.Ingredients.Where(i => i.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Stock, stock));

    /// <summary>用新的 DbContext 讀取最新庫存，避免讀到快取的舊值</summary>
    private async Task<Dictionary<int, decimal>> GetStocksAsync()
    {
        await using var db = fx.CreateContext();
        return await db.Ingredients.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.Stock);
    }

    private async Task<int> CountLogsAsync()
    {
        await using var db = fx.CreateContext();
        return await db.StockLogs.CountAsync();
    }
}