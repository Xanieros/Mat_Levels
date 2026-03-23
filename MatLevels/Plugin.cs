using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Game.Inventory;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Common.Component.Excel;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using MatLevels.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatLevels;

public sealed class Plugin : IDalamudPlugin
{

    private const string CommandName = "/mlconfig";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("MatLevels");
    private ConfigWindow ConfigWindow { get; init; }
    //private MainWindow MainWindow { get; init; }

    public ItemLevelTooltip ItemLevelTooltip { get; }
    public ItemLevelLookup ItemLevelLookup { get; }
    public RecipeLookup RecipeLookup { get; }
    public Hooks Hooks { get; }
    public List<RecipeData> Recipes { get; internal set; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Service.Initialize(pluginInterface);

        Configuration = Service.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ItemLevelTooltip = new ItemLevelTooltip(this);
        ItemLevelLookup = new ItemLevelLookup(this);
        RecipeLookup = new RecipeLookup(this);
        Hooks = new Hooks(this);

        Recipes = new List<RecipeData>();

        var ItemSheet = Service.DataManager.Excel.GetSheet<Item>();
        var RecipeSheet = Service.DataManager.Excel.GetSheet<Recipe>();
        var LevelTableSheet = Service.DataManager.Excel.GetSheet<RecipeLevelTable>();
        var craftTypeSheet = Service.DataManager.Excel.GetSheet<CraftType>();

        foreach ( var row in RecipeSheet)
        {
            if (row.RowId == 0 || row.ItemResult.RowId == 0) continue;

            var levelTableId = row.RecipeLevelTable.RowId;
            var craftTypeId = row.CraftType.RowId;

            Service.Log.Debug($"CraftTypeID: {craftTypeId}");

            var recipeDto = new RecipeData
            {
                RecipeId = row.RowId,
                ClassLevel = LevelTableSheet.GetRow(levelTableId).ClassJobLevel,
                JobClass = row.CraftType.RowId
            };

            for (int i = 0; i < row.Ingredient.Count; i++)
            {
                var ingredientItemId = row.Ingredient[i].RowId;
                if ( ingredientItemId == 0 || ingredientItemId > ItemSheet.Count) continue;

                var ingredientItem = ItemSheet.GetRow(ingredientItemId % 1000000);
                if (ingredientItem.RowId != 0 && ingredientItemId != uint.MaxValue)
                {
                    recipeDto.Ingredients.Add(new IngredientData
                    {
                        ItemId = ingredientItemId
                    });
                }
            }
            Recipes.Add(recipeDto);
        }

        // You might normally want to embed resources and load them from the manifest stream
        //var goatImagePath = Path.Combine(Service.PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        //MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        //WindowSystem.AddWindow(MainWindow);

        Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the configurations for MatLevels"
        });

        // Tell the UI system that we want our windows to be drawn through the window system
        Service.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        Service.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        // Adds another button doing the same but for the main ui of the plugin
        //Service.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [SamplePlugin] ===A cool log message from Sample Plugin===
        //Service.Log.Information($"===A cool log message from {Service.PluginInterface.Manifest.Name}===");


        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, ["Inventory", "InventoryLarge", "InventoryExpansion"], HandleInventoryUpdate);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "InventoryBuddy", HandleSaddlebagOpen);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, ["InventoryRetainer", "InventoryRetainerLarge"], HandleRetainerOpen);

        Service.ClientState.Login += ClientOnLogin;
    }

    public void Dispose()
    {
        // Unregister all actions to not leak anything during disposal of plugin
        Service.PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        //Service.PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        //MainWindow.Dispose();

        ItemLevelTooltip.Dispose();
        ItemLevelLookup.Dispose();
        Hooks.Dispose();

        Service.ClientState.Login -= ClientOnLogin;

        Service.CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // In response to the slash command, toggle the display status of our main ui
        ToggleConfigUi();
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    //public void ToggleMainUi() => MainWindow.Toggle();

    private void ClientOnLogin()
    {
        CheckInventories(InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4);
    }

    private DateTime lastCheckInventory = DateTime.MinValue;
    private void HandleInventoryUpdate(AddonEvent type, AddonArgs args)
    {
        //Service.Log.Debug($"In HandleInventoryUpdate with datetime: {lastCheckInventory}");
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

    public void ClearCache(int type = 0, int code = 0)
    {
        /*var ipl = ItemPriceLookup;
        ItemPriceLookup = new ItemPriceLookup(this);
        ipl.Dispose();*/
    }

    private void CheckInventories(params InventoryType[] inventoriesToScan)
    {
        if (Service.PlayerState.ContentId == 0) // || !ItemPriceLookup.CheckReady())
            return;
        /*if (!Configuration.PrefetchInventory)
            return;*/
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
