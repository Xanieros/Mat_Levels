using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MatLevels;

public class ItemLevelTooltip(Plugin plugin) : IDisposable
{
    private const int NodeId = 12726;

    public int? LastItemQuantity;

    public unsafe void OnItemTooltip(AtkUnitBase* itemTooltip)
    {
        var itemLevelData = plugin.ItemLevelLookup.Get(Service.GameGui.HoveredItem);
        var payloads = new List<Payload>();
        //payloads.Add(new TextPayload("Small test"));

        if (itemLevelData == null) return;

        payloads = ParseIlData(itemLevelData);

        //if (payloads.Count == 0) return;

        UpdateItemTooltip(itemTooltip, payloads);
    }

    private unsafe void UpdateItemTooltip(AtkUnitBase* itemTooltip, List<Payload> payloads)
    {
        if (payloads.Count == 0) return;

        AtkTextNode* testNode = null;
        for (var i = 0; i < itemTooltip->UldManager.NodeListCount; i++)
        {
            var node = itemTooltip->UldManager.NodeList[i];
            var nodeText = node->GetAsAtkTextNode()->GetText().AsReadOnlySeString();
            string[] allowedCategories = ["Metal", "Cloth", "Lumber", "Seafood", "Ingredient", "Stone", "Leather", "Bone", "Reagent"];

            //allowedCategories = plugin.Configuration.Categories.ToArray();   //implement later

            //Service.Log.Debug($"NodeID: {node->NodeId}, Text: {nodeText}");   
            if (node->NodeId == 35 && Array.IndexOf(allowedCategories, nodeText.ToString()) < 0 || payloads == null)
            {
                //Service.Log.Debug($"If statement returned true: {nodeText}");
                return;
            }
            //Service.Log.Debug($"If statement returned false!");
            if (node == null || node->NodeId != NodeId) {
                continue; 
            }
            testNode = (AtkTextNode*)node;
            break;
        }

        var insertNode = itemTooltip->GetNodeById(34);
        if (insertNode == null)
            return;
        var baseNode = itemTooltip->GetTextNodeById(34);
        if (testNode == null)
        {
            if (baseNode == null)
                return;
            testNode = IMemorySpace.GetUISpace()->Create<AtkTextNode>();
            testNode->AtkResNode.Type = NodeType.Text;
            testNode->AtkResNode.NodeId = NodeId;
            testNode->AtkResNode.NodeFlags = NodeFlags.AnchorRight | NodeFlags.AnchorTop;
            testNode->AtkResNode.X = 16;
            //testNode->AtkResNode.Width = 50;
            testNode->AtkResNode.Color = baseNode->AtkResNode.Color;
            testNode->TextColor = baseNode->TextColor;
            testNode->EdgeColor = baseNode->EdgeColor;
            testNode->LineSpacing = 18;
            testNode->FontSize = 12;
            testNode->TextFlags = baseNode->TextFlags;// | TextFlags.MultiLine | TextFlags.AutoAdjustNodeSize;

            var prev = insertNode->PrevSiblingNode;
            testNode->AtkResNode.ParentNode = insertNode->ParentNode;
            insertNode->PrevSiblingNode = (AtkResNode*)testNode;
            if (prev != null)
                prev->NextSiblingNode = (AtkResNode*)testNode;
            testNode->AtkResNode.PrevSiblingNode = prev;
            testNode->AtkResNode.NextSiblingNode = insertNode;
            itemTooltip->UldManager.UpdateDrawNodeList();
        }
        testNode->AtkResNode.ToggleVisibility(true);
        testNode->SetText(new SeString(payloads).Encode());
        testNode->ResizeNodeForCurrentText();
        testNode->SetHeight(0);
        //testNode->AtkResNode.SetYFloat(itemTooltip->WindowNode->AtkResNode.Height - 8);
        testNode->AtkResNode.SetYFloat(43);
        testNode->AtkResNode.SetXFloat(itemTooltip->WindowNode->AtkResNode.Width - 12 - testNode->AtkResNode.Width);
        //itemTooltip->WindowNode->SetHeight((ushort)(itemTooltip->WindowNode->AtkResNode.Height + testNode->AtkResNode.Height + 4));
        //itemTooltip->WindowNode->AtkResNode.SetHeight(itemTooltip->WindowNode->Height);
        //itemTooltip->WindowNode->Component->UldManager.RootNode->SetHeight(itemTooltip->WindowNode->Height);
        //itemTooltip->WindowNode->Component->UldManager.RootNode->PrevSiblingNode->SetHeight(itemTooltip->WindowNode->Height);
        //itemTooltip->RootNode->SetHeight(itemTooltip->WindowNode->Height);
        //var remainingSpace = ImGuiHelpers.MainViewport.WorkSize.Y - itemTooltip->Y - itemTooltip->GetScaledHeight(true) - 36;
        /*if (remainingSpace < 0)
        {
            plugin.Hooks.ItemDetailSetPositionPreservingOriginal(itemTooltip, itemTooltip->X, (short)(itemTooltip->Y + remainingSpace), 1);
        }*/

        //testNode->SetYFloat(baseNode->Y + testNode->AtkResNode.Height + 4);
    }

    public static unsafe void RestoreToNormal(AtkUnitBase* itemTooltip)
    {
        for (var i = 0; i < itemTooltip->UldManager.NodeListCount; i++)
        {
            var n = itemTooltip->UldManager.NodeList[i];
            if (n->NodeId != NodeId || !n->IsVisible())
                continue;
            n->ToggleVisibility(false);
            var insertNode = itemTooltip->GetNodeById(2);
            if (insertNode == null)
                return;
            itemTooltip->WindowNode->AtkResNode.SetHeight((ushort)(itemTooltip->WindowNode->AtkResNode.Height - n->Height - 4));
            itemTooltip->WindowNode->Component->UldManager.RootNode->SetHeight(itemTooltip->WindowNode->AtkResNode.Height);
            itemTooltip->WindowNode->Component->UldManager.RootNode->PrevSiblingNode->SetHeight(itemTooltip->WindowNode->AtkResNode.Height);
            insertNode->SetYFloat(insertNode->Y - n->Height - 4);
            break;
        }
    }

    private List<Payload> ParseIlData(ItemLevelData? ilData)
    {
        var payloads = new List<Payload>();
        if (ilData == null) return payloads;

        payloads.Add(new UIForegroundPayload(506));
        payloads.Add(new TextPayload($"{ilData.job}: {ilData.level}"));
        payloads.Add(new UIForegroundPayload(0));

        return payloads;
    }

    public void Refresh(IDictionary<uint, ItemLevelData> ilData)
    {
        if (Service.GameGui.HoveredItem >= 2000000) return;
        if (ilData.TryGetValue((uint)(Service.GameGui.HoveredItem % 1000000), out var data))
        {
            var newText = ParseIlData(data);
            Service.Framework.RunOnFrameworkThread(() => {
                try
                {
                    var tooltip = Service.GameGui.GetAddonByName("ItemDetail");
                    unsafe
                    {
                        if (tooltip.IsNull || !tooltip.IsVisible)
                            return;
                        RestoreToNormal((AtkUnitBase*)tooltip.Address);
                        UpdateItemTooltip((AtkUnitBase*)tooltip.Address, newText);
                    }
                }
                catch (Exception e)
                {
                    Service.Log.Error(e, "Failed to update tooltip");
                }
            });
        }
    }

    private void Cleanup()
    {
        unsafe
        {
            var atkUnitBase = (AtkUnitBase*)Service.GameGui.GetAddonByName("ItemDetail").Address;
            if (atkUnitBase == null)
                return;

            for (var n = 0; n < atkUnitBase->UldManager.NodeListCount; n++)
            {
                var node = atkUnitBase->UldManager.NodeList[n];
                if (node == null)
                    continue;
                if (node->NodeId != NodeId)
                    continue;
                if (node->ParentNode != null && node->ParentNode->ChildNode == node)
                    node->ParentNode->ChildNode = node->PrevSiblingNode;
                if (node->PrevSiblingNode != null)
                    node->PrevSiblingNode->NextSiblingNode = node->NextSiblingNode;
                if (node->NextSiblingNode != null)
                    node->NextSiblingNode->PrevSiblingNode = node->PrevSiblingNode;
                atkUnitBase->UldManager.UpdateDrawNodeList();
                node->Destroy(true);
                break;
            }
        }
    }
    public void FetchFailed(ICollection<uint> items)
    {
        if (!items.Contains((uint)Service.GameGui.HoveredItem % 1000000)) return;
        var newText = ParseIlData(null);
        Service.Framework.RunOnFrameworkThread(() => {
            try
            {
                var tooltip = Service.GameGui.GetAddonByName("ItemDetail");
                unsafe
                {
                    if (tooltip.IsNull || !tooltip.IsVisible)
                        return;
                    RestoreToNormal((AtkUnitBase*)tooltip.Address);
                    UpdateItemTooltip((AtkUnitBase*)tooltip.Address, newText);
                }
            }
            catch (Exception e)
            {
                Service.Log.Error(e, "Failed to update tooltip");
            }
        });
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    ~ItemLevelTooltip()
    {
        Cleanup();
    }
}

