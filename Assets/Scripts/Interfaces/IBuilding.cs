using UnityEngine;

[System.Serializable]
public struct BuildCost
{
    public string itemType;
    public int quantity;
}

public interface IBuildable
{
    string GetBuildingId();
    string GetDisplayName();
    Vector3 GetFootprintSize();
    BuildCost[] GetBuildCosts();
    void OnPlaced(GameObject placer);
}
