using UnityEngine;

public class Tree : MonoBehaviour, ITextInfoOverlay, IGatherable
{
    public int health = 5;
    [SerializeField] private GameObject logPrefab;
    [SerializeField] private int logDropCount = 1;
    [SerializeField] private float dropScatterRadius = 0.5f;
    [SerializeField] private Color depletedTint = new Color(0.35f, 0.35f, 0.35f, 1f);
    private int maxHealth;
    private int nextDropStep;
    private bool isDepleted;

    void Start()
    {
        maxHealth = Mathf.Max(4, health);
        health = maxHealth;
        nextDropStep = 1;
    }

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
        if (isDepleted || health <= 0)
        {
            return;
        }

        int oldHealth = health;
        health -= Mathf.Max(1, amount);
        health = Mathf.Max(0, health);
        Debug.Log("Tree took damage: " + amount);

        SpawnDropsForThresholds(oldHealth, health);

        if (health <= 0)
        {
            Deplete();
        }
    }

    void SpawnDropsForThresholds(int oldHealth, int newHealth)
    {
        int thresholdStepSize = Mathf.Max(1, maxHealth / 4);

        while (nextDropStep <= 4)
        {
            int thresholdHealth = Mathf.Max(0, maxHealth - (thresholdStepSize * nextDropStep));
            if (oldHealth > thresholdHealth && newHealth <= thresholdHealth)
            {
                DropLogs();
                nextDropStep++;
                continue;
            }

            break;
        }
    }

    void DropLogs()
    {
        if (logPrefab == null || logDropCount <= 0)
        {
            return;
        }

        for (int i = 0; i < logDropCount; i++)
        {
            Vector2 scatter = Random.insideUnitCircle * dropScatterRadius;
            Vector3 spawnPos = transform.position + new Vector3(scatter.x, 0.3f, scatter.y);
            Instantiate(logPrefab, spawnPos, Quaternion.identity);
        }
    }

    void Deplete()
    {
        if (isDepleted)
        {
            return;
        }
        isDepleted = true;

        ApplyDepletedVisual();

        // Depleted tree should no longer be considered a gather target.
        Destroy(this);
    }

    void ApplyDepletedVisual()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].GetPropertyBlock(propertyBlock);

            if (renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_BaseColor"))
            {
                propertyBlock.SetColor("_BaseColor", depletedTint);
            }

            if (renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_Color"))
            {
                propertyBlock.SetColor("_Color", depletedTint);
            }

            renderers[i].SetPropertyBlock(propertyBlock);
        }
    }

    public string GetInfoText()
    {
        return isDepleted ? "Tree\nDepleted" : $"Tree\nHP: {health}/{maxHealth}";
    }
}