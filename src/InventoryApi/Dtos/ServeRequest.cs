    using System.ComponentModel.DataAnnotations;

namespace InventoryApi.Dtos;

/// <summary>出餐請求：要出幾份</summary>
public class ServeRequest
{
    [Range(1, 999, ErrorMessage = "份數需介於 1～999")]
    public int Quantity { get; set; }
}