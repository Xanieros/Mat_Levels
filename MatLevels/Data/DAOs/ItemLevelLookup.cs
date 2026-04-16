using System;
using System.Collections.Generic;
using EasyCaching.InMemory;
using System.Threading;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;
using System.Collections.Concurrent;
using MatLevels.Core.Models;
using MatLevels.Core.Services;
using MatLevels.Plugin;

namespace MatLevels.Data.DAOs;
public class ItemLevelLookup : IDisposable
{
    private readonly MainPlugin plugin;
    private readonly InMemoryCaching cache = new("levels", new InMemoryCachingOptions { EnableReadDeepClone = false });
    private readonly ConcurrentQueue<uint> requestedItems = new();
    private readonly ConcurrentDictionary<uint, (Task Task, CancellationTokenSource Token)> activeTasks = new();
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public ItemLevelLookup(MainPlugin plugin)
    {
        this.plugin = plugin;
        Task.Run(ProcessQueue, cancellationTokenSource.Token).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnCanceled);
    }

    public bool CheckReady()
    {
        if (!Service.PlayerState.IsLoaded) return false;
        return true;
    }

    private static bool ToCraftMatItemId(ulong fullItemId, out uint itemId)
    {
        var sheet = Service.DataManager.Excel.GetSheet<Item>();
        itemId = (uint)(fullItemId % 1000000); 
        sheet ??= Service.DataManager.Excel.GetSheet<Item>();
        var tmp = sheet.GetRowOrDefault(itemId);
        if (tmp == null) return false;
        Item item = (Item)tmp;

        if ((item.ItemUICategory.RowId <= 46 || item.ItemUICategory.RowId > 54) && item.ItemUICategory.RowId != 45)
        {
            return false;
        }
        return sheet.GetRowOrDefault(itemId) is not { ItemSearchCategory.RowId: 0 };
    }

    public void Fetch(IEnumerable<uint> items)
    {
        foreach (var id in items)
        {
            if (!ToCraftMatItemId(id, out var itemId))
                continue;
            if (cache.Get(itemId.ToString()) != null || (activeTasks.TryGetValue(itemId, out var t) && !t.Task.IsFaulted))
                continue;
            if (!requestedItems.ToArray().Contains(itemId))
                requestedItems.Enqueue(itemId);
        }
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
