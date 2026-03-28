using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class StoneItem : MonoBehaviour, IPickable, ITextInfoOverlay
{
    [SerializeField] private string stoneType = "Stone";
    [SerializeField] private GameObject prefab;

    void Start()
    {
        IgnorePlayerCollision();
    }

    void IgnorePlayerCollision()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            return;
        }

        Collider[] stoneColliders = GetComponents<Collider>();
        Collider[] playerColliders = player.GetComponentsInChildren<Collider>();

        for (int i = 0; i < stoneColliders.Length; i++)
        {
            for (int j = 0; j < playerColliders.Length; j++)
            {
                Physics.IgnoreCollision(stoneColliders[i], playerColliders[j]);
            }
        }
    }

    public ItemData GetItemData()
    {
        return new ItemData(stoneType, 1, prefab);
    }

    public void OnPickup()
    {
        Destroy(gameObject);
    }

    public string GetInfoText()
    {
        return stoneType;
    }
}