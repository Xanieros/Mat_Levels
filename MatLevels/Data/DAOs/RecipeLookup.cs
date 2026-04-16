using MatLevels.Core.Models;
using MatLevels.Core.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MatLevels.Data.DAOs;

public class RecipeLookup()
{
    public async Task<Dictionary<uint, ItemLevelData>?> ScanRecipes(ICollection<uint> itemIds, List<RecipeData> Recipes)
    {
        try
        {
            var items = new Dictionary<uint, ItemLevelData>();
            foreach (var id in itemIds)
                items.Add(id, getMaxRecipe(id, Recipes, String.Empty, 0));
            
            return items;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex.ToString());
            return null;
        }
    }

    private ItemLevelData getMaxRecipe(uint id, List<RecipeData> Recipes, string jobName, int jobLevel)
    {
        foreach (var recipe in Recipes)
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.ItemId == id)
                {
                    var recipeData = getMaxRecipe(recipe.ItemId, Recipes, jobName, jobLevel);
                    if(recipeData.level > jobLevel)
                    {
                        jobName = recipeData.job;
                        jobLevel = recipeData.level;
                    }
                    
                    if ((int)recipe.ClassLevel > jobLevel)
                    {
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
                }
            }
        }
        
        return new ItemLevelData { job = jobName, level = jobLevel };
    }
}
