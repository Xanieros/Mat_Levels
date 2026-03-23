using Dalamud.Game.Gui.PartyFinder.Types;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Lumina.Excel;

namespace MatLevels;

public class RecipeLookup(Plugin plugin)
{
    public async Task<Dictionary<uint, ItemLevelData>?> ScanRecipes(ICollection<uint> itemIds, List<RecipeData> Recipes)
    {
        try
        {
            var items = new Dictionary<uint, ItemLevelData>();
            foreach (var id in itemIds)
            {
                string jobName = string.Empty;
                int jobLevel = 0;
                //var item = (plugin.ItemSheet.GetRowOrDefault(id);
                //Service.Log.Debug($"{item.Value.Name}");

                foreach (var recipe in Recipes)
                {
                    //Service.Log.Debug($"Recipe number(maybe): {recipe.RecipeId}, recipe ingredient count: {recipe.Ingredients.Count}");
                    foreach (var ingredient in recipe.Ingredients)
                    {
                        //RowRef<Item>? ingredient = null;

                        //Service.Framework.RunOnFrameworkThread(() => { ingredient = recipe.Ingredient[i]; }).Wait();

                        //if (ingredient == null) continue;
                        //Service.Log.Debug($"Ingredient[{recipe.Ingredients.IndexOf(ingredient)}]: {ingredient.ItemId}");

                        if (ingredient.ItemId == id)
                        {
                            //Service.Log.Debug($"Item1 RowId: {ingredient.ItemId}");
                            if ((int)recipe.ClassLevel > jobLevel)
                            {
                                //Service.Log.Debug($"I doube it's actually getting here...");
                                jobName = (int)recipe.JobClass switch
                                {
                                    0 => "CRP",
                                    1 => "BSM",
                                    2 => "ARM",
                                    3 => "GSM",
                                    4 => "LTW",
                                    5 => "WVR",
                                    6 => "ALC",
                                    7 => "CUL",
                                    _ => "NA"
                                };
                                jobLevel = (int)recipe.ClassLevel;
                            }
                            //Service.Log.Debug($"Item1 RowId: {ingredient.ItemId}");
                        }
                        //Service.Log.Debug($"(After if statements) Ingredient[{recipe.Ingredients.IndexOf(ingredient)}]: {ingredient.ItemId}");
                    }
                }

                items.Add(id, new ItemLevelData { job = jobName, level = jobLevel });
            }
            return items;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex.ToString());
            return null;
        }
    }
}
