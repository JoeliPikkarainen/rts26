using UnityEngine;

public enum WeaponType
{
    Sword,
    Axe,
    Spear,
    Bow,
    Other
}

public abstract class WeaponBase : MonoBehaviour, IEquipable, IPickable, ITextInfoOverlay
{
    [Header("Weapon")]
    [SerializeField] private string equipId = "weapon_base";
    [SerializeField] private string itemType = "Weapon";
    [SerializeField] private string displayName = "Weapon";
    [SerializeField] private WeaponType weaponType = WeaponType.Other;
    [SerializeField] private int damageBonus = 2;
    [SerializeField] private float attackSpeedMultiplier = 1.0f;
    [SerializeField] private GameObject equippedVisualPrefab;

    public string GetEquipId()
    {
        return equipId;
    }

    public string GetDisplayName()
    {
        return displayName;
    }

    public EquipSlot GetEquipSlot()
    {
        return EquipSlot.Weapon;
    }

    public WeaponType GetWeaponType()
    {
        return weaponType;
    }

    public int GetDamageBonus()
    {
        return damageBonus;
    }

    public float GetAttackSpeedMultiplier()
    {
        return Mathf.Max(0.1f, attackSpeedMultiplier);
    }

    public GameObject GetEquippedVisualPrefab()
    {
        return equippedVisualPrefab;
    }

    public ItemData GetItemData()
    {
        ItemData itemData = new ItemData(itemType, 1, gameObject);
        itemData.isEquipable = true;
        itemData.equipId = equipId;
        itemData.equipDisplayName = displayName;
        itemData.equipSlot = EquipSlot.Weapon;
        itemData.weaponType = weaponType;
        itemData.weaponDamageBonus = damageBonus;
        itemData.weaponAttackSpeedMultiplier = GetAttackSpeedMultiplier();
        return itemData;
    }

    public void OnPickup()
    {
        Destroy(gameObject);
    }

    public string GetInfoText()
    {
        return $"{displayName}\nDMG +{damageBonus}\nSPD x{GetAttackSpeedMultiplier():0.00}";
    }
}
