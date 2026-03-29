using Mirror;
using UnityEngine;

public class RockNode : NetworkBehaviour, ITextInfoOverlay, IGatherable
{
    [SerializeField] private int startHealth = 8;
    [SerializeField] private GameObject stonePrefab;
    [SerializeField] private int stoneDropCount = 1;
    [SerializeField] private float dropScatterRadius = 0.4f;
    [SerializeField] private Color depletedTint = new Color(0.25f, 0.25f, 0.25f, 1f);

    [SyncVar(hook = nameof(OnHealthSync))]
    private int health;

    [SyncVar(hook = nameof(OnIsDepletedSync))]
    private bool isDepleted;

    private int maxHealth;
    private int nextDropStep;

    public GameObject GetDropPrefab()
    {
        return stonePrefab;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        maxHealth = Mathf.Max(4, startHealth);
        health = maxHealth;
        nextDropStep = 1;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        maxHealth = Mathf.Max(4, startHealth);
        if (isDepleted) ApplyDepletedVisual();
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
        if (!isServer) return;
        if (hit.dst != gameObject && (hit.dst == null || !hit.dst.transform.IsChildOf(transform)))
        {
            return;
        }

        Gather(hit.ctx.dmg);
    }

    public void Gather(int amount)
    {
        if (!isServer) return;
        if (isDepleted || health <= 0) return;

        int oldHealth = health;
        health -= Mathf.Max(1, amount);
        health = Mathf.Max(0, health);

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
                DropStone();
                nextDropStep++;
                continue;
            }

            break;
        }
    }

    void DropStone()
    {
        if (stonePrefab == null || stoneDropCount <= 0) return;

        for (int i = 0; i < stoneDropCount; i++)
        {
            Vector2 scatter = Random.insideUnitCircle * dropScatterRadius;
            Vector3 spawnPos = transform.position + new Vector3(scatter.x, 0.25f, scatter.y);
            GameObject stone = Instantiate(stonePrefab, spawnPos, Quaternion.identity);
            NetworkServer.Spawn(stone);
        }
    }

    void Deplete()
    {
        if (isDepleted) return;
        isDepleted = true; // SyncVar — hook fires on all clients
    }

    void OnHealthSync(int _, int __) { /* health bar update could go here */ }

    void OnIsDepletedSync(bool _, bool newVal)
    {
        if (newVal) ApplyDepletedVisual();
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
        return isDepleted ? "Rock\nDepleted" : $"Rock\nHP: {health}/{maxHealth}";
    }
}
