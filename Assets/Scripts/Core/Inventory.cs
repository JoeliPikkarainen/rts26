using UnityEngine;
using System.Collections.Generic;

public class Inventory : MonoBehaviour
{
    [SerializeField] private int maxTotalItemCount = 0; // 0 means unlimited
    [SerializeField] private int maxUniqueItemTypeCount = 0; // 0 means unlimited

    // Dictionary to store items by type - different log types stack separately
    private Dictionary<string, ItemData> items = new Dictionary<string, ItemData>();

    void OnEnable()
    {
        GameEvents.OnPickup += HandlePickup;
    }

    void OnDisable()
    {
        GameEvents.OnPickup -= HandlePickup;
    }

    void HandlePickup(PickupEvent pickup)
    {
        // Only add to inventory if it's the player picking up
        if (pickup.src != gameObject) return;

        AddItem(pickup.itemData);
    }

    public void AddItem(ItemData itemData)
    {
        if (itemData == null || itemData.quantity <= 0)
        {
            return;
        }

        if (!CanAddItem(itemData))
        {
            Debug.Log($"Inventory full. Could not add {itemData.itemType} x{itemData.quantity}");
            return;
        }

        if (items.ContainsKey(itemData.itemType))
        {
            // Stack with existing item type
            items[itemData.itemType].quantity += itemData.quantity;
        }
        else
        {
            // New item type
            items[itemData.itemType] = itemData;
        }

        Debug.Log("Inventory: " + itemData.itemType + " x" + items[itemData.itemType].quantity);
    }

    public ItemData GetItem(string itemType)
    {
        if (items.ContainsKey(itemType))
        {
            return items[itemType];
        }
        return null;
    }

    public bool HasItem(string itemType, int quantity = 1)
    {
        return items.ContainsKey(itemType) && items[itemType].quantity >= quantity;
    }

    public bool CanAddItem(ItemData itemData)
    {
        if (itemData == null || itemData.quantity <= 0)
        {
            return false;
        }

        if (!items.ContainsKey(itemData.itemType) && maxUniqueItemTypeCount > 0 && items.Count >= maxUniqueItemTypeCount)
        {
            return false;
        }

        int remainingCapacity = GetRemainingCapacity();
        if (remainingCapacity < 0)
        {
            return true;
        }

        return itemData.quantity <= remainingCapacity;
    }

    public bool IsFull()
    {
        return maxTotalItemCount > 0 && GetTotalItemCount() >= maxTotalItemCount;
    }

    public int GetTotalItemCount()
    {
        int total = 0;
        foreach (ItemData item in items.Values)
        {
            if (item != null && item.quantity > 0)
            {
                total += item.quantity;
            }
        }

        return total;
    }

    public int GetRemainingCapacity()
    {
        if (maxTotalItemCount <= 0)
        {
            return -1;
        }

        return Mathf.Max(0, maxTotalItemCount - GetTotalItemCount());
    }

    public void SetMaxTotalItemCount(int value)
    {
        maxTotalItemCount = Mathf.Max(0, value);
    }

    public int GetMaxTotalItemCount()
    {
        return maxTotalItemCount;
    }

    public void SetMaxUniqueItemTypeCount(int value)
    {
        maxUniqueItemTypeCount = Mathf.Max(0, value);
    }

    public int GetMaxUniqueItemTypeCount()
    {
        return maxUniqueItemTypeCount;
    }

    public int GetUniqueItemTypeCount()
    {
        return items.Count;
    }

    public void RemoveItem(string itemType, int quantity = 1)
    {
        if (items.ContainsKey(itemType))
        {
            items[itemType].quantity -= quantity;
            if (items[itemType].quantity <= 0)
            {
                items.Remove(itemType);
            }
            Debug.Log("Removed " + itemType + " x" + quantity);
        }
    }

    public List<ItemData> GetAllItems()
    {
        return new List<ItemData>(items.Values);
    }
}
