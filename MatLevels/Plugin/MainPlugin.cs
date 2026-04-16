using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using MatLevels.Configurations;
using MatLevels.Core.Models;
using MatLevels.Core.Services;
using MatLevels.Data.DAOs;
using MatLevels.Features.ItemLevelTooltip;
using MatLevels.UI.Windows;
using RecipeLookup = MatLevels.Data.DAOs.RecipeLookup;
using System;
using System.Collections.Generic;

namespace MatLevels.Plugin;

public sealed class MainPlugin : IDalamudPlugin
{

    private const string CommandName = "/mlconfig";
    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("MatLevels");
    private ConfigWindow ConfigWindow { get; init; }
    public ItemLevelTooltip ItemLevelTooltip { get; }
    public ItemLevelLookup ItemLevelLookup { get; }
    public RecipeLookup RecipeLookup { get; }
    public Hooks Hooks { get; }
    public List<RecipeData> Recipes { get; internal set; }

    public MainPlugin(IDalamudPluginInterface pluginInterface)
    {
        Service.Initialize(pluginInterface);

        Configuration = Service.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (Configuration.AllowedCategories.Count == 0)
        {
            Configuration.AllowedCategories.Add(new Category { Name = "Metal", IsAllowed = true });
            Configuration.AllowedCategories.Add(new Category { Name = "Cloth", IsAllowed = true });
            Configuration.AllowedCategories.Add(new Category { Name = "Lumber", IsAllowed = true });
            Configuration.AllowedCategories.Add(new Category { Name = "Seafood", IsAllowed = true });
            Configuration.AllowedCategories.Add(new Category { Name = "Ingredient", IsAllowed = true });
            Configuration.AllowedCategories.Add(new Category { Name = "Stone", IsAllowed = true });
            Configuration.AllowedCategories.Add(new Category { Name = "Leather", IsAllowed = true });
            Configuration.AllowedCategories.Add(new Category { Name = "Bone", IsAllowed = true });
            Configuration.AllowedCategories.Add(new Category { Name = "Reagent", IsAllowed = true });
        }

        ItemLevelTooltip = new ItemLevelTooltip(this);
        ItemLevelLookup = new ItemLevelLookup(this);
        RecipeLookup = new RecipeLookup();
        Hooks = new Hooks(this);

        Recipes = MatLevels.Core.Services.RecipeLookupService.GetAllRecipeData();

        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(ConfigWindow);

        Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the configurations for MatLevels"
        });

        Service.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        Service.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, ["Inventory", "InventoryLarge", "InventoryExpansion"], HandleInventoryUpdate);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "InventoryBuddy", HandleSaddlebagOpen);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, ["InventoryRetainer", "InventoryRetainerLarge"], HandleRetainerOpen);

        Service.ClientState.Login += ClientOnLogin;
    }

    public void Dispose()
    {
        Service.PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();

        ItemLevelTooltip.Dispose();
        ItemLevelLookup.Dispose();
        Hooks.Dispose();

        Service.ClientState.Login -= ClientOnLogin;

        Service.CommandManager.RemoveHandler(CommandName);
    }

    public void RefreshOnCategoryChange()
    {
        ClientOnLogin();
    }

    private void OnCommand(string command, string args)
    {
        ToggleConfigUi();
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();

    private void ClientOnLogin()
    {
        CheckInventories(InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4);
    }

    private DateTime lastCheckInventory = DateTime.MinValue;
    private void HandleInventoryUpdate(AddonEvent type, AddonArgs args)
    {
        if ((DateTime.Now - lastCheckInventory).TotalMinutes < 1) return;
        CheckInventories(InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4);
        lastCheckInventory = DateTime.Now;
    }

    private DateTime lastCheckSaddlebag = DateTime.MinValue;
    private void HandleSaddlebagOpen(AddonEvent type, AddonArgs args)
    {
        if ((DateTime.Now - lastCheckSaddlebag).TotalSeconds < 30) return;
        CheckInventories(InventoryType.SaddleBag1, InventoryType.SaddleBag2, InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2);
        lastCheckSaddlebag = DateTime.Now;
    }

    private DateTime lastCheckRetainer = DateTime.MinValue;
    private void HandleRetainerOpen(AddonEvent type, AddonArgs args)
    {
        if ((DateTime.Now - lastCheckRetainer).TotalSeconds < 5) return;
        CheckInventories(InventoryType.RetainerPage1, InventoryType.RetainerPage2, InventoryType.RetainerPage3, InventoryType.RetainerPage4,
            InventoryType.RetainerPage5, InventoryType.RetainerPage6, InventoryType.RetainerPage7);
        lastCheckRetainer = DateTime.Now;
    }

    private void CheckInventories(params InventoryType[] inventoriesToScan)
    {
        if (Service.PlayerState.ContentId == 0)
            return;
        if (!Configuration.PrefetchInventory)
            return;
        Service.Log.Debug($"Prefetch: checking {inventoriesToScan.Length} inventories");
        try
        {
            var items = new HashSet<uint>();
            unsafe
            {
                var manager = InventoryManager.Instance();
                foreach (var inv in inventoriesToScan)
                {
                    var container = manager->GetInventoryContainer(inv);
                    if (container == null || !container->IsLoaded)
                        continue;
                    for (var i = 0; i < container->Size; i++)
                    {
                        var item = &container->Items[i];
                        var itemId = item->ItemId;
                        if (itemId != 0)
                            items.Add(itemId);
                    }
                }
            }

            if (items.Count > 0)
            {
                Service.Log.Debug($"Prefetch: queueing {items.Count} items");
                ItemLevelLookup.Fetch(items);
            }
        }
        catch (Exception e)
        {
            Service.Log.Error(e, "Failed to process update");
        }
    }
}
