using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryApi.Migrations
{
    /// <inheritdoc />
    public partial class RestrictIngredientDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeItems_Ingredients_IngredientId",
                table: "RecipeItems");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeItems_Ingredients_IngredientId",
                table: "RecipeItems",
                column: "IngredientId",
                principalTable: "Ingredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeItems_Ingredients_IngredientId",
                table: "RecipeItems");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeItems_Ingredients_IngredientId",
                table: "RecipeItems",
                column: "IngredientId",
                principalTable: "Ingredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
