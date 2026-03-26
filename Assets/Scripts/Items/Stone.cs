using UnityEngine;

public class StoneItem : MonoBehaviour, IPickable, ITextInfoOverlay
{
    [SerializeField] private string stoneType = "Stone";
    private GameObject prefab;

    void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = 1f;
        rb.linearDamping = 0.2f;
        rb.angularDamping = 0.1f;
        rb.constraints = RigidbodyConstraints.None;

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
        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>("Prefabs/Stone");
        }

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