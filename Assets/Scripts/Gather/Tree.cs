using UnityEngine;

public class Tree : MonoBehaviour, ITextInfoOverlay
{
    public int health = 5;
    [SerializeField] private GameObject logPrefab;
    [SerializeField] private int logDropCount = 1;

    void OnEnable()
    {
        GameEvents.OnHit += HandleHit;
    }

    void OnDisable()
    {
        GameEvents.OnHit -= HandleHit;
    }

    void HandleHit(HitEvent hit)
    {
        if (hit.dst != gameObject) return;

        health -= hit.ctx.dmg;

        Debug.Log("Tree took damage: " + hit.ctx.dmg);

        if (health <= 0)
        {
            Fall();
        }
    }

    void Fall()
    {
        // Add rigidbody and make tree fall
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.AddForce(transform.forward * 5f, ForceMode.Impulse);

        // Drop logs
        if (logPrefab != null)
        {
            for (int i = 0; i < logDropCount; i++)
            {
                Vector3 spawnPos = transform.position + Random.insideUnitSphere * 0.5f;
                Instantiate(logPrefab, spawnPos, Quaternion.identity);
            }
        }
    }

    public string GetInfoText()
    {
        return $"Tree\nHP: {health}";
    }
}