using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using MatLevels.Configurations;
using MatLevels.Plugin;

namespace MatLevels.UI.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private MainPlugin plugin;

    public ConfigWindow(MainPlugin plugin) : base("Mat Level Configurations")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse |
                ImGuiWindowFlags.NoResize;

        Size = new Vector2(232, 360);
        SizeCondition = ImGuiCond.Always;

        this.plugin = plugin;
        configuration = this.plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Allow Moving Window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }
        var prefetch = configuration.PrefetchInventory;
        if (ImGui.Checkbox("Prefetch Inventory Data", ref prefetch))
        {
            configuration.PrefetchInventory = prefetch;
            configuration.Save();
        }
        var categories = configuration.AllowedCategories;
        ImGui.Text("Category List:");
        if (ImGui.BeginChild("CategoryList", new System.Numerics.Vector2(0, 247), true))
        {
            bool onChange = false;
            foreach (var category in categories)
            {
                bool value = category.IsAllowed;
                ImGui.Checkbox(category.Name, ref value);
                if (value == category.IsAllowed)
                    continue;
                else
                {
                    category.IsAllowed = value;
                    onChange = true;
                }
            }

            if (onChange)
                plugin.RefreshOnCategoryChange();

            ImGui.EndChild();
            configuration.Save();
        }
    }
}
