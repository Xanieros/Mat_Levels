using System;
using System.Collections.Generic;
using EasyCaching.InMemory;
using System.Text;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Threading;
using System.Threading.Tasks;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System.Collections.Concurrent;

namespace MatLevels;
public class ItemLevelLookup : IDisposable
{
    private readonly Plugin plugin;
    private readonly InMemoryCaching cache = new("levels", new InMemoryCachingOptions { EnableReadDeepClone = false });
    private readonly ConcurrentQueue<uint> requestedItems = new();
    private readonly ConcurrentDictionary<uint, (Task Task, CancellationTokenSource Token)> activeTasks = new();
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public ItemLevelLookup(Plugin plugin)
    {
        this.plugin = plugin;
        Task.Run(ProcessQueue, cancellationTokenSource.Token).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnCanceled);
    }

    public bool CheckReady()
    {
        if (!Service.PlayerState.IsLoaded) return false;
        return true;
    }

    private static bool ToCraftMatItemId(ulong fullItemId, out uint itemId)//, ExcelSheet<Item>? sheet = null)
    {
        var sheet = Service.DataManager.Excel.GetSheet<Item>();
        //Service.Log.Debug($"fullItemId: {fullItemId}");
        itemId = (uint)(fullItemId % 1000000); //skips HQ
        sheet ??= Service.DataManager.Excel.GetSheet<Item>();
        var tmp = sheet.GetRowOrDefault(itemId);
        if (tmp == null) return false;
        Item item = (Item)tmp;
        //Service.Log.Debug($"Item id: {itemId}, category row id: {item.ItemUICategory.RowId}");
        //Service.Log.Debug($"itemid: {itemId}, name hopefully: {item.Name}, rowid: {item.ItemUICategory.RowId}");

        if ((item.ItemUICategory.RowId <= 46 || item.ItemUICategory.RowId > 54) && item.ItemUICategory.RowId != 45) //9 categories
        {
            Service.Log.Debug($"Skipped itemid: {itemId}, name hopefully: {item.Name}, rowid: {item.ItemUICategory.RowId}");
            return false;
        }
        Service.Log.Debug($"Added itemid: {itemId}, name hopefully: {item.Name}, rowid: {item.ItemUICategory.RowId}");
        return sheet.GetRowOrDefault(itemId) is not { ItemSearchCategory.RowId: 0 };
    }

    public void Fetch(IEnumerable<uint> items)
    {
        //var itemSheet = Service.DataManager.Excel.GetSheet<Item>();
        foreach (var id in items)
        {
            if (!ToCraftMatItemId(id, out var itemId))//, itemSheet))
                continue;
            if (cache.Get(itemId.ToString()) != null || (activeTasks.TryGetValue(itemId, out var t) && !t.Task.IsFaulted))
                continue;
            if (!requestedItems.ToArray().Contains(itemId))
                requestedItems.Enqueue(itemId);
        }
        Service.Log.Debug($"Made it to end of Fetch {requestedItems.Count}");
    }

    private async Task ProcessQueue()
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        while (await timer.WaitForNextTickAsync(cancellationTokenSource.Token))
        {
            if (requestedItems.IsEmpty)
                continue;
            var items = new HashSet<uint>();
            while (items.Count < 50 && requestedItems.TryDequeue(out var item))
                items.Add(item);
            await FetchInternal(items);
        }

        timer.Dispose();
    }

    private Task<Dictionary<uint, ItemLevelData>?> FetchInternal(ICollection<uint> itemIds)
    {
        var token = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
        var itemTask = FetchItemTask();

        foreach (var id in itemIds)
        {
            var task = Task.Run(async () => {
                var items = await itemTask;
                if (items != null && items.TryGetValue(id, out var value))
                    cache.Set(id.ToString(), value, TimeSpan.FromMinutes(90));
                activeTasks.TryRemove(id, out _);
            }, token.Token);
            task.ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnCanceled);
            activeTasks[id] = (task, token);
        }

        return itemTask;

        async Task<Dictionary<uint, ItemLevelData>?> FetchItemTask()
        {
            var fetchStart = DateTime.Now;
            var result = await plugin.RecipeLookup.ScanRecipes(itemIds, plugin.Recipes);
            if (result != null)
                plugin.ItemLevelTooltip.Refresh(result);
            else
                plugin.ItemLevelTooltip.FetchFailed(itemIds);
            Service.Log.Debug($"Fetching {itemIds.Count} items took {(DateTime.Now - fetchStart).TotalMilliseconds:F0}ms");
            return result;
        }
    }

    public ItemLevelData? Get(ulong fullItemId)
    {
        if (!ToCraftMatItemId(fullItemId, out var itemId))
            return (null);

        if (cache.Get<ItemLevelData>(itemId.ToString()) is { IsNull: false, Value: var ilData })
            return (ilData);
        if (activeTasks.TryGetValue(itemId, out var t))
            return (null);
        

        requestedItems.Enqueue(itemId);

        return (null);
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
    }
}
