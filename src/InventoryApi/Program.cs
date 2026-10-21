using InventoryApi.Services;
using InventoryApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// 註冊 EF Core，連線字串從 User Secrets 的 ConnectionStrings:Default 讀取
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
// 註冊庫存服務；Scoped：每個 HTTP 請求建立一個實例，與 DbContext 的生命週期一致
builder.Services.AddScoped<InventoryService>();    
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
