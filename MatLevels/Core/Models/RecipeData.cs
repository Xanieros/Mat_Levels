using System.Collections.Generic;

namespace MatLevels.Core.Models;

public class RecipeData
{
    public uint RecipeId { get; set; }
    public uint ItemId { get; set; }
    public List<IngredientData> Ingredients { get; set; } = new();
    public uint ClassLevel { get; set; }
    public uint JobClass {  get; set; }
}

public class IngredientData
{
    public uint ItemId { get; set; }
}
