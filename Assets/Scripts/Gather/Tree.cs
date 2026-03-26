using UnityEngine;

public class Tree : MonoBehaviour, ITextInfoOverlay, IGatherable
{
    public int health = 5;
    [SerializeField] private GameObject logPrefab;
    [SerializeField] private int logDropCount = 1;
    private bool hasFallen;

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

        Gather(hit.ctx.dmg);
    }

    public void Gather(int amount)
    {
        if (hasFallen || health <= 0)
        {
            return;
        }

        health -= Mathf.Max(1, amount);
        Debug.Log("Tree took damage: " + amount);

        if (health <= 0)
        {
            Fall();
        }
    }

    void Fall()
    {
        if (hasFallen)
        {
            return;
        }
        hasFallen = true;

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

        // Depleted tree should no longer be considered a gather target.
        Destroy(this);
    }

    public string GetInfoText()
    {
        return $"Tree\nHP: {health}";
    }
}