# 餐飲進銷存系統：ASP.NET Core 版

以 **C# / ASP.NET Core 10** 重新實作 [Spring Boot 版後端](https://github.com/AtayalLin/restaurant-inventory-system-api) 的核心庫存邏輯：出餐時依配方在交易中扣除所有食材，並以樂觀鎖處理多人同時操作。

> 這是 [餐飲進銷存系統](https://github.com/AtayalLin/restaurant-inventory-system-React) 的 .NET 練習版本，目的是驗證同一套業務邏輯能從 Java 轉換到 .NET 生態系。

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoftsqlserver&logoColor=white)
![xUnit](https://img.shields.io/badge/tests-xUnit%205%20passed-success)

## 功能

| API | 說明 |
|---|---|
| `GET /api/ingredients` | 列出全部食材 |
| `GET /api/ingredients/{id}` | 查詢單一食材 |
| `POST /api/ingredients` | 新增食材（欄位驗證、名稱不可重複） |
| `PUT /api/ingredients/{id}` | 修改食材（樂觀鎖，需帶 `rowVersion`） |
| `DELETE /api/ingredients/{id}` | 刪除食材（配方使用中則拒絕） |
| `POST /api/products/{id}/serve` | **出餐：依配方整筆扣庫存** |

錯誤回應統一使用 RFC 9110 的 ProblemDetails 格式（`application/problem+json`）。

## 設計重點

### 1. 出餐扣庫存：全部成功，或全部不扣

出餐時先檢查配方中**所有**食材，收集全部不足項目一次回傳（`422`），而不是遇到第一個就停止。全部足夠才扣除，並在**同一個資料庫交易**中寫入庫存異動紀錄。

```json
// POST /api/products/1/serve  { "quantity": 100 }
{
  "title": "食材庫存不足",
  "status": 422,
  "shortages": [
    { "name": "高麗菜", "required": 15000.00, "available": 4400.00 },
    { "name": "豬肉片", "required": 10000.00, "available": 2800.00 }
  ]
}
```

### 2. 樂觀鎖：兩道防線

食材使用 SQL Server 的 `rowversion` 欄位，每次更新自動變更版本號。

| 防線 | 擋下的情況 |
|---|---|
| 程式比對版本 | 前端拿著過期版本送出 |
| 資料庫 `UPDATE … WHERE RowVersion = ?` | 讀取之後、寫入之前，剛好被別人修改 |

**為什麼需要第一道防線**：測試時發現，用過期版本送出「與現有資料完全相同」的內容時，EF Core 會判斷沒有欄位變更而**不送出 UPDATE**，資料庫層的版本檢查因此被跳過，請求反而回傳 200。所以在程式層加上明確的版本比對。

出餐時若發生衝突，會清除快取、重新讀取最新庫存再判斷，最多重試 3 次。

### 3. 刪除保護

EF Core 預設的外鍵是 `ON DELETE CASCADE`，誤刪一個食材時，所有用到它的配方都會被一起刪掉。因此改為 `Restrict`，並在程式中先檢查、回傳清楚的 `409` 訊息，資料庫的外鍵限制作為第二道防線。

### 4. 機密管理

- 資料庫密碼放在 `.env`（Docker 使用）與 .NET **User Secrets**（API 使用），都不進版本控制
- 以 `dotnet list package --vulnerable --include-transitive` 掃描相依套件，修正了 SQL Server 驅動間接引用的有漏洞套件

## 與 Spring Boot 版的對照

| 概念 | Spring Boot 版 | ASP.NET Core 版 |
|---|---|---|
| Web API | `@RestController` | `[ApiController]` + `ControllerBase` |
| 依賴注入 | 建構子注入 | Primary constructor 注入 |
| ORM | Spring Data JPA | Entity Framework Core 10 |
| 交易 | `@Transactional` | 單次 `SaveChanges()` 的隱含交易 |
| 樂觀鎖 | `UPDATE … WHERE version = ?` | `[Timestamp]` rowversion |
| 錯誤格式 | `ProblemDetail` | `ProblemDetails` |
| 資料庫遷移 | `ddl-auto` | EF Core Migrations |
| 測試 | JUnit + H2 | xUnit + SQL Server 測試資料庫 |

## 測試

5 個 xUnit 整合測試，連線到真正的 SQL Server（獨立的 `InventoryDb_Test` 資料庫，執行前重建、結束後刪除）：

| 測試 | 驗證 |
|---|---|
| 庫存足夠 | 三種食材正確扣除，寫入 3 筆異動紀錄 |
| 兩種食材不足 | 列出所有不足項目，**庫存完全沒扣** |
| 商品不存在 | 回傳 ProductNotFound |
| 兩個請求同時出餐，庫存足夠 | 兩筆都成功，**沒有少扣** |
| 兩個請求同時出餐，只夠一份 | 剛好一筆成功，**庫存不會變負數** |

```bash
dotnet test
```

## 本機執行

需求：.NET 10 SDK、Docker Desktop

```bash
# 1. 設定資料庫密碼（至少 8 碼，含大小寫、數字、符號）
cp .env.example .env        # 再編輯 .env 填入密碼

# 2. 啟動 SQL Server
docker compose up -d

# 3. 設定 API 的連線字串（密碼與 .env 相同）
dotnet user-secrets set "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=InventoryDb;User Id=sa;Password=你的密碼;TrustServerCertificate=True" \
  --project src/InventoryApi

# 4. 建立資料表與種子資料
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/InventoryApi

# 5. 啟動 API（http://localhost:5245）
dotnet run --project src/InventoryApi
```

`src/InventoryApi/InventoryApi.http` 收錄了所有 API 的測試請求，搭配 VS Code 的 REST Client 擴充即可直接送出。

## 專案結構

```
src/InventoryApi/
├─ Controllers/   IngredientsController（CRUD）、ServeController（出餐）
├─ Services/      InventoryService：出餐扣庫存的業務邏輯
├─ Models/        Ingredient、Product、RecipeItem、StockLog
├─ Dtos/          請求 DTO 與欄位驗證
├─ Data/          AppDbContext、關聯設定、種子資料
└─ Migrations/    EF Core 資料庫遷移
tests/InventoryApi.Tests/
├─ DatabaseFixture.cs        測試資料庫的建立與清除
└─ InventoryServiceTests.cs  出餐扣庫存的整合測試
```

## 相關專案

- [前端：React 19 + TypeScript](https://github.com/AtayalLin/restaurant-inventory-system-React)（[線上 Demo](https://atayallin.github.io/restaurant-inventory-system-React/)）
- [後端：Spring Boot 3.5 + MySQL](https://github.com/AtayalLin/restaurant-inventory-system-api)