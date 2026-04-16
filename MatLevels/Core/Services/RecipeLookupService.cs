using Lumina.Excel.Sheets;
using MatLevels.Core.Models;
using System.Collections.Generic;

namespace MatLevels.Core.Services;

internal class RecipeLookupService
{
    internal static List<RecipeData> GetAllRecipeData ()
    {
        var recipeList = new List<RecipeData>();
        var ItemSheet = Service.DataManager.Excel.GetSheet<Item>();
        var RecipeSheet = Service.DataManager.Excel.GetSheet<Recipe>();
        var LevelTableSheet = Service.DataManager.Excel.GetSheet<RecipeLevelTable>();
        foreach (var row in RecipeSheet)
        {
            if (row.RowId == 0 || row.ItemResult.RowId == 0) continue;

            var levelTableId = row.RecipeLevelTable.RowId;
            var craftTypeId = row.CraftType.RowId;

            var recipeDto = new RecipeData
            {
                RecipeId = row.RowId,
                ItemId = row.ItemResult.Value.RowId,
                ClassLevel = LevelTableSheet.GetRow(levelTableId).ClassJobLevel,
                JobClass = row.CraftType.RowId
            };

            for (int i = 0; i < row.Ingredient.Count; i++)
            {
                var ingredientItemId = row.Ingredient[i].RowId;
                if (ingredientItemId == 0 || ingredientItemId > ItemSheet.Count) continue;

                var ingredientItem = ItemSheet.GetRow(ingredientItemId % 1000000);
                if (ingredientItem.RowId != 0 && ingredientItemId != uint.MaxValue)
                {
                    recipeDto.Ingredients.Add(new IngredientData
                    {
                        ItemId = ingredientItemId
                    });
                }
            }
            recipeList.Add(recipeDto);
        }
        return recipeList;
    }
}
