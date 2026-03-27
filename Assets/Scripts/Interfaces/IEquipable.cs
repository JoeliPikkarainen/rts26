public enum EquipSlot
{
    Weapon,
    Chest,
    Legs
}

public interface IEquipable
{
    string GetEquipId();
    string GetDisplayName();
    EquipSlot GetEquipSlot();
}
