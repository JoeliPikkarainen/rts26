using UnityEngine;

public class ItemData
{
    public string itemType; // e.g., "Log", "Stone", "Wood"
    public int quantity;
    public GameObject prefab; // Reference to the prefab for rebuilding/dropping
    
    public ItemData(string itemType, int quantity, GameObject prefab)
    {
        this.itemType = itemType;
        this.quantity = quantity;
        this.prefab = prefab;
    }
    
    public override string ToString()
    {
        return $"{itemType} x{quantity}";
    }
}
