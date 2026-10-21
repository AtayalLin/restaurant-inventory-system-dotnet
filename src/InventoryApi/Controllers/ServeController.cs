using InventoryApi.Dtos;
using InventoryApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace InventoryApi.Controllers;

/// <summary>出餐 API：POST /api/products/{id}/serve</summary>
[ApiController]
[Route("api/products")]
public class ServeController(InventoryService inventory) : ControllerBase
{
    [HttpPost("{id:int}/serve")]
    public async Task<IActionResult> Serve(int id, ServeRequest req)
    {
        var result = await inventory.ServeAsync(id, req.Quantity);

        if (result.Status == ServeStatus.ProductNotFound)
            return NotFound();

        if (result.Status == ServeStatus.InsufficientStock)
        {
            // 422：請求格式正確，但業務規則不允許；附上所有不足項目給前端顯示
            var problem = new ProblemDetails { Status = 422, Title = "食材庫存不足" };
            problem.Extensions["shortages"] = result.Shortages;
            return UnprocessableEntity(problem);
        }

        if (result.Status == ServeStatus.Conflict)
            return Problem(statusCode: 409, title: "庫存正在被其他人更新，請稍後再試");

        return Ok(new { message = "出餐完成" });
    }
}