using UnityEngine;

public class LogItem : MonoBehaviour, IPickable, ITextInfoOverlay
{
    [SerializeField] private string logType = "Log"; // e.g., "Oak Log", "Pine Log"
    [SerializeField] private GameObject prefab;

    void Start()
    {
        IgnorePlayerCollision();
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