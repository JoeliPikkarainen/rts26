using UnityEngine;
using System;
using System.Collections.Generic;

public class CraftingTableBuilding : MonoBehaviour, IBuildable, ITextInfoOverlay
{
    [Serializable]
    private class CraftCostEntry
    {
        public GameObject resourcePrefab;
        public int quantity = 1;
        public string fallbackItemType;
    }

    [Serializable]
    private class CraftableItemEntry
    {
        public string displayName;
        public GameObject itemPrefab;
        public int quantity = 1;
        public CraftCostEntry[] costs;
    }

    [SerializeField] private string buildingId = "crafting_table";
    [SerializeField] private string displayName = "Crafting Table";
    [SerializeField] private Vector3 footprintSize = new Vector3(2f, 1f, 2f);
    [SerializeField] private BuildCost[] buildCosts = new BuildCost[]
    {
        new BuildCost { itemType = "Log", quantity = 10 },
        new BuildCost { itemType = "Stone", quantity = 4 }
    };
    [Header("Crafting")]
    [SerializeField] private List<CraftableItemEntry> craftableItems = new List<CraftableItemEntry>();

    private GameObject openedByPlayer;

    public bool IsOpenBy(GameObject player)
    {
        return openedByPlayer != null && openedByPlayer == player;
    }

    public bool TryOpen(GameObject player)
    {
        if (player == null)
        {
            return false;
        }

        Inventory playerInventory = player.GetComponent<Inventory>();
        if (playerInventory == null)
        {
            return false;
        }

        if (openedByPlayer != null && openedByPlayer != player)
        {
            return false;
        }

        openedByPlayer = player;
        return true;
    }

    public void CloseTable(GameObject player)
    {
        if (openedByPlayer == null)
        {
            return;
        }

        if (player != null && openedByPlayer != player)
        {
            return;
        }

        openedByPlayer = null;
    }

    void OnDisable()
    {
        openedByPlayer = null;
    }

    void OnGUI()
    {
        if (openedByPlayer == null)
        {
            return;
        }

        Inventory playerInventory = openedByPlayer.GetComponent<Inventory>();
        if (playerInventory == null)
        {
            CloseTable(openedByPlayer);
            return;
        }

        DrawCraftingWindow(playerInventory);
    }

    void DrawCraftingWindow(Inventory playerInventory)
    {
        float width = 640f;
        float height = 440f;
        Rect window = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(window, displayName + " (E/Esc to close)");

        if (craftableItems.Count == 0)
        {
            GUI.Label(new Rect(window.x + 12f, window.y + 34f, width - 24f, 24f), "No craftable items configured.");
        }

        float y = window.y + 34f;
        float rowHeight = 52f;
        float maxY = window.y + height - 50f;

        for (int i = 0; i < craftableItems.Count; i++)
        {
            if (y + rowHeight > maxY)
            {
                GUI.Label(new Rect(window.x + 12f, y, width - 24f, 24f), "...");
                break;
            }

            CraftableItemEntry entry = craftableItems[i];
            string itemLabel = GetCraftingItemDisplayName(entry);
            string costsLabel = FormatCosts(entry.costs);
            bool canCraft = CanAfford(playerInventory, entry.costs) && CanStoreCraftedItem(playerInventory, entry);

            GUI.Label(new Rect(window.x + 12f, y, width * 0.36f, 24f), itemLabel);
            GUI.Label(new Rect(window.x + 12f, y + 20f, width * 0.52f, 24f), costsLabel);

            GUI.enabled = canCraft;
            if (GUI.Button(new Rect(window.x + width - 88f, y + 10f, 72f, 30f), "Craft"))
            {
                CraftItem(playerInventory, entry);
            }
            GUI.enabled = true;

            y += rowHeight;
        }

        if (GUI.Button(new Rect(window.x + width - 88f, window.y + height - 36f, 72f, 24f), "Close"))
        {
            CloseTable(openedByPlayer);
        }
    }

    bool CanStoreCraftedItem(Inventory inventory, CraftableItemEntry entry)
    {
        if (inventory == null)
        {
            return false;
        }

        if (!TryBuildCraftedItemData(entry, out ItemData craftedItem))
        {
            return false;
        }

        return inventory.CanAddItem(craftedItem);
    }

    void CraftItem(Inventory playerInventory, CraftableItemEntry entry)
    {
        if (playerInventory == null)
        {
            return;
        }

        if (!CanAfford(playerInventory, entry.costs))
        {
            return;
        }

        if (!TryBuildCraftedItemData(entry, out ItemData craftedItem))
        {
            return;
        }

        if (!playerInventory.CanAddItem(craftedItem))
        {
            return;
        }

        ConsumeCosts(playerInventory, entry.costs);
        playerInventory.AddItem(craftedItem);

        if (craftedItem.isEquipable && playerInventory.GetEquippedItem(craftedItem.equipSlot) == null)
        {
            playerInventory.TryEquipItem(craftedItem.itemType);
        }
        else
        {
            IEquipable equipable = GetEquipableFromPrefab(craftedItem.prefab);
            if (equipable != null && playerInventory.GetEquippedItem(equipable.GetEquipSlot()) == null)
            {
                playerInventory.TryEquipItem(craftedItem.itemType);
            }
        }
    }

    bool TryBuildCraftedItemData(CraftableItemEntry entry, out ItemData craftedItem)
    {
        craftedItem = null;

        if (entry == null || entry.itemPrefab == null)
        {
            return false;
        }

        int quantity = Mathf.Max(1, entry.quantity);
        IPickable pickable = entry.itemPrefab.GetComponent<IPickable>() ?? entry.itemPrefab.GetComponentInChildren<IPickable>();
        if (pickable != null)
        {
            ItemData template = pickable.GetItemData();
            if (template != null)
            {
                craftedItem = new ItemData(template.itemType, quantity, template.prefab);
                craftedItem.CopyRuntimeMetadataFrom(template);
                if (string.IsNullOrWhiteSpace(craftedItem.itemType))
                {
                    craftedItem.itemType = entry.itemPrefab.name;
                }

                if (craftedItem.prefab == null)
                {
                    craftedItem.prefab = entry.itemPrefab;
                }
                return true;
            }
        }

        craftedItem = new ItemData(entry.itemPrefab.name, quantity, entry.itemPrefab);
        return true;
    }

    IEquipable GetEquipableFromPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        IEquipable equipable = prefab.GetComponent<IEquipable>();
        if (equipable != null)
        {
            return equipable;
        }

        return prefab.GetComponentInChildren<IEquipable>();
    }

    bool CanAfford(Inventory inventory, CraftCostEntry[] costs)
    {
        if (inventory == null || costs == null)
        {
            return true;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            if (costs[i] == null || costs[i].quantity <= 0)
            {
                continue;
            }

            if (!TryResolveCostItemType(costs[i], out string itemType))
            {
                continue;
            }

            if (!inventory.HasItem(itemType, costs[i].quantity))
            {
                return false;
            }
        }

        return true;
    }

    void ConsumeCosts(Inventory inventory, CraftCostEntry[] costs)
    {
        if (inventory == null || costs == null)
        {
            return;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            if (costs[i] == null || costs[i].quantity <= 0)
            {
                continue;
            }

            if (!TryResolveCostItemType(costs[i], out string itemType))
            {
                continue;
            }

            inventory.RemoveItem(itemType, costs[i].quantity);
        }
    }

    string GetCraftingItemDisplayName(CraftableItemEntry entry)
    {
        if (entry == null)
        {
            return "Unknown";
        }

        if (!string.IsNullOrWhiteSpace(entry.displayName))
        {
            return entry.displayName;
        }

        if (entry.itemPrefab == null)
        {
            return "Missing Prefab";
        }

        IEquipable equipable = GetEquipableFromPrefab(entry.itemPrefab);
        if (equipable != null)
        {
            return equipable.GetDisplayName();
        }

        return entry.itemPrefab.name;
    }

    string FormatCosts(CraftCostEntry[] costs)
    {
        if (costs == null || costs.Length == 0)
        {
            return "Cost: Free";
        }

        List<string> parts = new List<string>();
        for (int i = 0; i < costs.Length; i++)
        {
            if (costs[i] == null || costs[i].quantity <= 0)
            {
                continue;
            }

            if (!TryResolveCostItemType(costs[i], out string itemType))
            {
                if (costs[i].resourcePrefab != null)
                {
                    parts.Add($"{costs[i].resourcePrefab.name} x{costs[i].quantity}");
                }
                continue;
            }

            parts.Add($"{itemType} x{costs[i].quantity}");
        }

        return parts.Count == 0 ? "Cost: Free" : "Cost: " + string.Join(", ", parts);
    }

    bool TryResolveCostItemType(CraftCostEntry cost, out string itemType)
    {
        itemType = string.Empty;

        if (cost == null)
        {
            return false;
        }

        if (cost.resourcePrefab != null)
        {
            IPickable pickable = cost.resourcePrefab.GetComponent<IPickable>() ?? cost.resourcePrefab.GetComponentInChildren<IPickable>();
            if (pickable != null)
            {
                ItemData data = pickable.GetItemData();
                if (data != null && !string.IsNullOrWhiteSpace(data.itemType))
                {
                    itemType = data.itemType;
                    return true;
                }
            }

            itemType = cost.resourcePrefab.name;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(cost.fallbackItemType))
        {
            itemType = cost.fallbackItemType;
            return true;
        }

        return false;
    }

    public string GetBuildingId()
    {
        return buildingId;
    }

    public string GetDisplayName()
    {
        return displayName;
    }

    public Vector3 GetFootprintSize()
    {
        return footprintSize;
    }

    public BuildCost[] GetBuildCosts()
    {
        return buildCosts;
    }

    public void OnPlaced(GameObject placer)
    {
        string placerName = placer != null ? placer.name : "Unknown";
        Debug.Log($"Building placed: {displayName} by {placerName}");
    }

    public string GetInfoText()
    {
        string useState = openedByPlayer == null ? "(E) Craft" : "In use";
        return $"{displayName}\n{useState}\nRecipes: {craftableItems.Count}";
    }
}
