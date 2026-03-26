using UnityEngine;
using System.Collections.Generic;

public class Inventory : MonoBehaviour
{
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
