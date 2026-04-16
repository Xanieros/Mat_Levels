using Dalamud.Configuration;
using MatLevels.Core.Services;
using System;
using MatLevels.Core.Models;
using System.Collections.Generic;

namespace MatLevels.Configurations;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool PrefetchInventory { get; set; } = true;
    public List<Category> AllowedCategories { get; set; } = new List<Category>();

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        Service.PluginInterface.SavePluginConfig(this);
    }
}
