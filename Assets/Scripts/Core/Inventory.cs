using UnityEngine;
using System.Collections.Generic;
using System;

public class Inventory : MonoBehaviour
{
    [SerializeField] private int maxTotalItemCount = 0; // 0 means unlimited
    [SerializeField] private int maxUniqueItemTypeCount = 0; // 0 means unlimited

    // Dictionary to store items by type - different log types stack separately
    private Dictionary<string, ItemData> items = new Dictionary<string, ItemData>();

    private ItemData equippedWeapon;
    private ItemData equippedChest;
    private ItemData equippedLegs;

    public event Action OnEquipmentChanged;

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

    public ItemData GetEquippedItem(EquipSlot slot)
    {
        if (slot == EquipSlot.Weapon)
        {
            return equippedWeapon;
        }

        if (slot == EquipSlot.Chest)
        {
            return equippedChest;
        }

        if (slot == EquipSlot.Legs)
        {
            return equippedLegs;
        }

        return null;
    }

    public bool TryEquipItem(string itemType)
    {
        if (string.IsNullOrWhiteSpace(itemType) || !HasItem(itemType, 1))
        {
            return false;
        }

        ItemData inventoryItem = GetItem(itemType);
        if (inventoryItem == null)
        {
            return false;
        }

        if (!TryGetEquipSlotFromItemData(inventoryItem, out EquipSlot slot))
        {
            return false;
        }

        ItemData currentlyEquipped = GetEquippedItem(slot);

        if (currentlyEquipped != null)
        {
            if (!CanAddItem(currentlyEquipped))
            {
                return false;
            }

            AddItemReference(currentlyEquipped);
        }

        if (!TryTakeItemReference(itemType, 1, out ItemData equippedReference))
        {
            return false;
        }

        SetEquippedItem(slot, equippedReference);
        OnEquipmentChanged?.Invoke();
        return true;
    }

    public bool UnequipSlot(EquipSlot slot)
    {
        ItemData equippedItem = GetEquippedItem(slot);
        if (equippedItem == null)
        {
            return false;
        }

        if (!CanAddItem(equippedItem))
        {
            return false;
        }

        AddItemReference(equippedItem);
        SetEquippedItem(slot, null);
        OnEquipmentChanged?.Invoke();
        return true;
    }

    public bool TryMoveItemTo(Inventory targetInventory, string itemType, int quantity)
    {
        if (targetInventory == null || targetInventory == this || string.IsNullOrWhiteSpace(itemType) || quantity <= 0)
        {
            return false;
        }

        if (!TryTakeItemReference(itemType, quantity, out ItemData movedItem))
        {
            return false;
        }

        if (!targetInventory.CanAddItem(movedItem))
        {
            AddItemReference(movedItem);
            return false;
        }

        targetInventory.AddItemReference(movedItem);
        return true;
    }

    public bool TryMoveAllOfTypeTo(Inventory targetInventory, string itemType)
    {
        ItemData item = GetItem(itemType);
        if (item == null)
        {
            return false;
        }

        return TryMoveItemTo(targetInventory, itemType, item.quantity);
    }

    IEquipable GetEquipableFromItemData(ItemData itemData)
    {
        if (itemData == null || itemData.prefab == null)
        {
            return null;
        }

        IEquipable equipable = itemData.prefab.GetComponent<IEquipable>();
        if (equipable != null)
        {
            return equipable;
        }

        return itemData.prefab.GetComponentInChildren<IEquipable>();
    }

    bool TryGetEquipSlotFromItemData(ItemData itemData, out EquipSlot slot)
    {
        slot = default;

        if (itemData == null)
        {
            return false;
        }

        if (itemData.isEquipable)
        {
            slot = itemData.equipSlot;
            return true;
        }

        IEquipable equipable = GetEquipableFromItemData(itemData);
        if (equipable == null)
        {
            return false;
        }

        slot = equipable.GetEquipSlot();
        return true;
    }

    void SetEquippedItem(EquipSlot slot, ItemData itemData)
    {
        if (slot == EquipSlot.Weapon)
        {
            equippedWeapon = itemData;
            return;
        }

        if (slot == EquipSlot.Chest)
        {
            equippedChest = itemData;
            return;
        }

        if (slot == EquipSlot.Legs)
        {
            equippedLegs = itemData;
        }
    }

    bool TryTakeItemReference(string itemType, int quantity, out ItemData movedItem)
    {
        movedItem = null;

        if (!items.TryGetValue(itemType, out ItemData sourceItem) || sourceItem == null || quantity <= 0 || sourceItem.quantity < quantity)
        {
            return false;
        }

        if (sourceItem.quantity == quantity)
        {
            items.Remove(itemType);
            movedItem = sourceItem;
            return true;
        }

        sourceItem.quantity -= quantity;
        movedItem = new ItemData(sourceItem.itemType, quantity, sourceItem.prefab);
        movedItem.CopyRuntimeMetadataFrom(sourceItem);
        return true;
    }

    void AddItemReference(ItemData itemData)
    {
        if (itemData == null || itemData.quantity <= 0)
        {
            return;
        }

        if (items.TryGetValue(itemData.itemType, out ItemData existing))
        {
            existing.quantity += itemData.quantity;
            if (itemData.isEquipable)
            {
                existing.CopyRuntimeMetadataFrom(itemData);
            }
            return;
        }

        items[itemData.itemType] = itemData;
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
