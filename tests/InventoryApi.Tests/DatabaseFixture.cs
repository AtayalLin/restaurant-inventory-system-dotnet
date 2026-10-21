using InventoryApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace InventoryApi.Tests;

/// <summary>
/// 測試專用資料庫：與開發用的 InventoryDb 分開，名稱為 InventoryDb_Test。
/// 執行測試前砍掉重建，結束後刪除，不影響手動測試的資料。
/// </summary>
public class DatabaseFixture : IDisposable
{
    public string ConnectionString { get; }

    public DatabaseFixture()
    {
        // 讀取 API 專案的 User Secrets，密碼不寫在測試程式碼裡
        var config = new ConfigurationBuilder().AddUserSecrets<AppDbContext>().Build();
        var devConnection = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("找不到 ConnectionStrings:Default，請先設定 User Secrets");

        // 把資料庫名稱換成測試專用的
        ConnectionString = devConnection.Replace("Database=InventoryDb;", "Database=InventoryDb_Test;");

        // 安全檢查：確定已經換成測試資料庫，才允許執行下面的「刪除資料庫」，避免誤刪開發資料
        if (!ConnectionString.Contains("Database=InventoryDb_Test;"))
            throw new InvalidOperationException("連線字串未切換到測試資料庫，已中止以保護開發資料");

        using var db = CreateContext();
        db.Database.EnsureDeleted();   // 刪除上次殘留的測試資料庫
        db.Database.Migrate();         // 套用與正式相同的遷移，種子資料也會一起建立
    }

    /// <summary>每次呼叫都建立新的 DbContext，模擬不同的 HTTP 請求</summary>
    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    public void Dispose()
    {
        using var db = CreateContext();
        db.Database.EnsureDeleted();   // 測試結束後清除測試資料庫
    }
}