using InventoryApi.Data;
using InventoryApi.Dtos;
using InventoryApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Controllers;

/// <summary>食材 CRUD API，路由為 /api/ingredients</summary>
[ApiController]                   // 自動驗證 DTO，失敗時回傳 400 ProblemDetails
[Route("api/[controller]")]
public class IngredientsController(AppDbContext db) : ControllerBase   // AppDbContext 由 DI 注入
{
    // GET /api/ingredients：列出全部食材（AsNoTracking：唯讀查詢，不追蹤變更，效能較好）
    [HttpGet]
    public async Task<List<Ingredient>> GetAll() =>
        await db.Ingredients.AsNoTracking().OrderBy(i => i.Id).ToListAsync();

    // GET /api/ingredients/1：查詢單一食材
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Ingredient>> GetById(int id)
    {
        var ingredient = await db.Ingredients.FindAsync(id);
        return ingredient is null ? NotFound() : ingredient;
    }

    // POST /api/ingredients：新增食材
    [HttpPost]
    public async Task<ActionResult<Ingredient>> Create(IngredientRequest req)
    {
        // 先檢查名稱是否重複，回傳清楚的 409，而不是讓資料庫唯一索引拋出 500
        if (await db.Ingredients.AnyAsync(i => i.Name == req.Name.Trim()))
            return Problem(statusCode: 409, title: "食材名稱已存在");

        var ingredient = new Ingredient();
        req.ApplyTo(ingredient);
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();

        // 201 Created，並在 Location 標頭附上新資源的網址
        return CreatedAtAction(nameof(GetById), new { id = ingredient.Id }, ingredient);
    }

    // PUT /api/ingredients/1：修改食材（含樂觀鎖檢查）
    [HttpPut("{id:int}")]
    public async Task<ActionResult<Ingredient>> Update(int id, IngredientRequest req)
    {
        if (req.RowVersion is null)
            return Problem(statusCode: 400, title: "修改時必須提供 rowVersion");

        var ingredient = await db.Ingredients.FindAsync(id);
        if (ingredient is null) return NotFound();

        // 第一道檢查：前端送來的版本和資料庫目前的版本不同，代表資料已被別人改過。
        // 少了這段時，若送來的值與現有資料完全相同，EF Core 會判斷「沒有變更」而不送出 UPDATE，
        // 樂觀鎖的 WHERE 條件也就不會執行，過期的請求反而會回 200。
        if (!ingredient.RowVersion.SequenceEqual(req.RowVersion))
            return Problem(statusCode: 409, title: "資料已被其他人修改，請重新整理後再試");

        // 第二道檢查：把「前端當時看到的版本」設為原始值；
        // SaveChanges 時 EF Core 會產生 UPDATE … WHERE Id = @id AND RowVersion = @原始值，
        // 擋下「讀取之後、寫入之前」剛好被別人改掉的情況
        db.Entry(ingredient).Property(i => i.RowVersion).OriginalValue = req.RowVersion;
        req.ApplyTo(ingredient);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // 影響 0 筆：代表這段期間資料已被別人修改
            return Problem(statusCode: 409, title: "資料已被其他人修改，請重新整理後再試");
        }

        return ingredient;
    }

    // DELETE /api/ingredients/1：刪除食材（配方使用中則拒絕）
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ingredient = await db.Ingredients.FindAsync(id);
        if (ingredient is null) return NotFound();

        // 程式先擋一次，回傳清楚的訊息；資料庫的 RESTRICT 是第二道防線
        if (await db.RecipeItems.AnyAsync(r => r.IngredientId == id))
            return Problem(statusCode: 409, title: "此食材仍被商品配方使用，無法刪除");

        db.Ingredients.Remove(ingredient);
        await db.SaveChangesAsync();
        return NoContent();   // 204：刪除成功，不回傳內容
    }
}