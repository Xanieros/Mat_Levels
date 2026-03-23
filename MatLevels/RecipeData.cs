using System;
using System.Collections.Generic;
using System.Text;

namespace MatLevels;

public class RecipeData
{
    public uint RecipeId { get; set; }
    public List<IngredientData> Ingredients { get; set; } = new();
    public uint ClassLevel { get; set; }
    public uint JobClass {  get; set; }
}

public class IngredientData
{
    public uint ItemId { get; set; }
}
