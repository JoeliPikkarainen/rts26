using UnityEngine;

public class ItemData
{
    public string itemType; // e.g., "Log", "Stone", "Wood"
    public int quantity;
    public GameObject prefab; // Reference to the prefab for rebuilding/dropping

    // Optional metadata used by equip systems when prefab references are not available.
    public bool isEquipable;
    public string equipId;
    public string equipDisplayName;
    public EquipSlot equipSlot;
    public WeaponType weaponType = WeaponType.Other;
    public int weaponDamageBonus;
    public float weaponAttackSpeedMultiplier = 1f;
    
    public ItemData(string itemType, int quantity, GameObject prefab)
    {
        this.itemType = itemType;
        this.quantity = quantity;
        this.prefab = prefab;
    }

    public void CopyRuntimeMetadataFrom(ItemData other)
    {
        if (other == null)
        {
            return;
        }

        isEquipable = other.isEquipable;
        equipId = other.equipId;
        equipDisplayName = other.equipDisplayName;
        equipSlot = other.equipSlot;
        weaponType = other.weaponType;
        weaponDamageBonus = other.weaponDamageBonus;
        weaponAttackSpeedMultiplier = other.weaponAttackSpeedMultiplier;
    }
    
    public override string ToString()
    {
        return $"{itemType} x{quantity}";
    }
}
