using UnityEngine;
using System.Collections.Generic;

public class StorageChestBuilding : MonoBehaviour, IBuildable, ITextInfoOverlay
{
    [SerializeField] private string buildingId = "storage_chest";
    [SerializeField] private string displayName = "Storage Chest";
    [SerializeField] private Vector3 footprintSize = new Vector3(1.25f, 1f, 1.25f);
    [Header("Storage")]
    [SerializeField] private int maxStorageAmount = 200;
    [SerializeField] private int maxUniqueItemAmount = 24;
    [SerializeField] private BuildCost[] buildCosts = new BuildCost[]
    {
        new BuildCost { itemType = "Log", quantity = 1 },
        new BuildCost { itemType = "Stone", quantity = 1 }
    };

    private Inventory chestInventory;
    private GameObject openedByPlayer;

    public bool IsOpenBy(GameObject player)
    {
        return openedByPlayer != null && openedByPlayer == player;
    }

    public Inventory GetStorageInventory()
    {
        return chestInventory;
    }

    public bool DepositAllFrom(Inventory sourceInventory)
    {
        if (sourceInventory == null || chestInventory == null)
        {
            return false;
        }

        List<ItemData> sourceItems = sourceInventory.GetAllItems();
        bool movedAny = false;

        for (int i = 0; i < sourceItems.Count; i++)
        {
            ItemData item = sourceItems[i];
            if (item == null || item.quantity <= 0 || string.IsNullOrWhiteSpace(item.itemType))
            {
                continue;
            }

            if (!chestInventory.CanAddItem(new ItemData(item.itemType, item.quantity, item.prefab)))
            {
                continue;
            }

            chestInventory.AddItem(new ItemData(item.itemType, item.quantity, item.prefab));
            sourceInventory.RemoveItem(item.itemType, item.quantity);
            movedAny = true;
        }

        return movedAny;
    }

    void Awake()
    {
        chestInventory = GetComponent<Inventory>();
        if (chestInventory == null)
        {
            chestInventory = gameObject.AddComponent<Inventory>();
        }

        ApplyStorageLimits();
    }

    void OnValidate()
    {
        maxStorageAmount = Mathf.Max(0, maxStorageAmount);
        maxUniqueItemAmount = Mathf.Max(0, maxUniqueItemAmount);

        if (chestInventory == null)
        {
            chestInventory = GetComponent<Inventory>();
        }

        ApplyStorageLimits();
    }

    void ApplyStorageLimits()
    {
        if (chestInventory == null)
        {
            return;
        }

        chestInventory.SetMaxTotalItemCount(maxStorageAmount);
        chestInventory.SetMaxUniqueItemTypeCount(maxUniqueItemAmount);
    }

    void OnDisable()
    {
        openedByPlayer = null;
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

    public void CloseChest(GameObject player)
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

    void OnGUI()
    {
        if (openedByPlayer == null)
        {
            return;
        }

        Inventory playerInventory = openedByPlayer != null ? openedByPlayer.GetComponent<Inventory>() : null;
        if (playerInventory == null || chestInventory == null)
        {
            CloseChest(openedByPlayer);
            return;
        }

        DrawChestWindow(playerInventory);
    }

    void DrawChestWindow(Inventory playerInventory)
    {
        float width = 820f;
        float height = 460f;
        Rect window = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(window, displayName + " (E to interact, Esc to close)");

        float panelPadding = 12f;
        float panelWidth = (window.width - panelPadding * 3f) * 0.5f;
        float panelHeight = window.height - 72f;

        Rect playerPanel = new Rect(window.x + panelPadding, window.y + 34f, panelWidth, panelHeight);
        Rect chestPanel = new Rect(playerPanel.xMax + panelPadding, playerPanel.y, panelWidth, panelHeight);

        DrawInventoryPanel(playerPanel, "Player Inventory", playerInventory, chestInventory, "Deposit");
        DrawInventoryPanel(chestPanel, "Chest Inventory", chestInventory, playerInventory, "Withdraw");

        if (GUI.Button(new Rect(window.x + window.width - 86f, window.y + window.height - 36f, 72f, 24f), "Close"))
        {
            CloseChest(openedByPlayer);
        }
    }

    void DrawInventoryPanel(Rect panelRect, string title, Inventory sourceInventory, Inventory targetInventory, string transferVerb)
    {
        GUI.Box(panelRect, title);

        List<ItemData> items = sourceInventory.GetAllItems();
        float rowY = panelRect.y + 26f;
        float rowHeight = 26f;
        float maxY = panelRect.y + panelRect.height - 30f;

        if (items.Count == 0)
        {
            GUI.Label(new Rect(panelRect.x + 8f, rowY, panelRect.width - 16f, 24f), "Empty");
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (rowY + rowHeight > maxY)
            {
                GUI.Label(new Rect(panelRect.x + 8f, rowY, panelRect.width - 16f, 24f), "...");
                break;
            }

            GUI.Label(new Rect(panelRect.x + 8f, rowY, panelRect.width * 0.45f, 24f), item.itemType + " x" + item.quantity);

            if (GUI.Button(new Rect(panelRect.x + panelRect.width * 0.48f, rowY, panelRect.width * 0.22f, 22f), transferVerb + " 1"))
            {
                TransferOne(sourceInventory, targetInventory, item.itemType);
            }

            if (GUI.Button(new Rect(panelRect.x + panelRect.width * 0.72f, rowY, panelRect.width * 0.22f, 22f), transferVerb + " All"))
            {
                TransferAll(sourceInventory, targetInventory, item.itemType);
            }

            rowY += rowHeight;
        }
    }

    void TransferOne(Inventory from, Inventory to, string itemType)
    {
        if (from == null || to == null || string.IsNullOrWhiteSpace(itemType))
        {
            return;
        }

        if (!from.HasItem(itemType, 1))
        {
            return;
        }

        ItemData item = from.GetItem(itemType);
        if (item == null)
        {
            return;
        }

        if (!to.CanAddItem(new ItemData(item.itemType, 1, item.prefab)))
        {
            return;
        }

        to.AddItem(new ItemData(item.itemType, 1, item.prefab));
        from.RemoveItem(itemType, 1);
    }

    void TransferAll(Inventory from, Inventory to, string itemType)
    {
        if (from == null || to == null || string.IsNullOrWhiteSpace(itemType))
        {
            return;
        }

        ItemData item = from.GetItem(itemType);
        if (item == null || item.quantity <= 0)
        {
            return;
        }

        int quantityToMove = item.quantity;
        if (!to.CanAddItem(new ItemData(item.itemType, quantityToMove, item.prefab)))
        {
            return;
        }

        to.AddItem(new ItemData(item.itemType, quantityToMove, item.prefab));
        from.RemoveItem(itemType, quantityToMove);
    }

    int CountStacks(Inventory inventory)
    {
        if (inventory == null)
        {
            return 0;
        }

        return inventory.GetAllItems().Count;
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
        string useState = openedByPlayer == null ? "(E) Open storage" : "In use";
        return $"{displayName}\n{useState}\nStacks: {CountStacks(chestInventory)}";
    }
}
