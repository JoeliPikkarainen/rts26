using UnityEngine;

public class LogItem : MonoBehaviour, IPickable, ITextInfoOverlay
{
    [SerializeField] private string logType = "Log"; // e.g., "Oak Log", "Pine Log"
    private GameObject prefab;

    void Start()
    {
        Debug.Log("LogItem created");

        // Rotate 90 degrees on X axis
        transform.rotation = Quaternion.Euler(90, 0, 0);
        
        // Setup rigidbody for falling
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        // Configure rigidbody
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = 1f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.constraints = RigidbodyConstraints.None;
        
        // Immediately ignore collision with player
        IgnorePlayerCollision();
        
        Debug.Log("Log rigidbody configured - isKinematic: " + rb.isKinematic + ", useGravity: " + rb.useGravity);
    }

    void IgnorePlayerCollision()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Collider[] logColliders = GetComponents<Collider>();
            Collider[] playerColliders = player.GetComponents<Collider>();
            
            foreach (Collider logCollider in logColliders)
            {
                foreach (Collider playerCollider in playerColliders)
                {
                    Physics.IgnoreCollision(logCollider, playerCollider);
                }
            }
            
            Debug.Log("Ignored collision between log and player - log colliders: " + logColliders.Length + ", player colliders: " + playerColliders.Length);
        }
        else
        {
            Debug.Log("Player is null, cannot ignore collision with log");
        }
    }

    public ItemData GetItemData()
    {
        // Store the prefab reference for inventory
        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>("Prefabs/Log"); // Adjust path as needed
        }
        
        return new ItemData(logType, 1, prefab);
    }

    public void OnPickup()
    {
        Debug.Log("Picked up " + logType);
        Destroy(gameObject);
    }

    public string GetInfoText()
    {
        return $"{logType}";
    }
}