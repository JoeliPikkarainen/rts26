using UnityEngine;

public class SimpleCubeBuilding : MonoBehaviour, IBuildable, ITextInfoOverlay
{
    [SerializeField] private string buildingId = "demo_cube";
    [SerializeField] private string displayName = "Demo Cube";
    [SerializeField] private Vector3 footprintSize = new Vector3(1f, 1f, 1f);
    [SerializeField] private BuildCost[] buildCosts = new BuildCost[]
    {
        new BuildCost { itemType = "Log", quantity = 1 }
    };

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
        Debug.Log($"Building placed: {displayName} by {placer.name}");
    }

    public string GetInfoText()
    {
        return $"{displayName}\n(B)uilding";
    }
}
