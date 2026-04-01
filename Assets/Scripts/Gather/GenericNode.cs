using Mirror;
using UnityEngine;

public class GenericNode : NetworkBehaviour, ITextInfoOverlay, IGatherable
{
    [Header("Identity")]
    [SerializeField] private string nodeName = "Resource";
    [SerializeField] private GatherResourcePreference resourcePreference = GatherResourcePreference.Closest;

    [Header("Durability")]
    [SerializeField] private int startHealth = 8;

    [Header("Drops")]
    [SerializeField] private GameObject dropPrefab;
    [SerializeField] private int dropCount = 1;
    [SerializeField] private float dropHeight = 0.25f;
    [SerializeField] private float dropScatterRadius = 0.4f;

    [Header("Visuals")]
    [SerializeField] private Color depletedTint = new Color(0.25f, 0.25f, 0.25f, 1f);

    [SyncVar(hook = nameof(OnHealthSync))]
    private int health;

    [SyncVar(hook = nameof(OnIsDepletedSync))]
    private bool isDepleted;

    private int maxHealth;
    private int nextDropStep;

    public GameObject GetDropPrefab()
    {
        return dropPrefab;
    }

    public GatherResourcePreference GetResourcePreference()
    {
        return resourcePreference;
    }

    public bool IsDepleted()
    {
        return isDepleted || health <= 0;
    }

    public bool MatchesPreference(GatherResourcePreference preference)
    {
        return preference == GatherResourcePreference.Closest || preference == resourcePreference;
    }

    protected void ApplyIdentityDefaults(string defaultName, GatherResourcePreference defaultPreference)
    {
        if (string.IsNullOrWhiteSpace(nodeName) || nodeName == "Resource")
        {
            nodeName = defaultName;
        }

        if (resourcePreference == GatherResourcePreference.Closest)
        {
            resourcePreference = defaultPreference;
        }
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
                DropItems();
                nextDropStep++;
                continue;
            }

            break;
        }
    }

    [Server]
    void DropItems()
    {
        if (dropPrefab == null || dropCount <= 0) return;

        for (int i = 0; i < dropCount; i++)
        {
            Vector2 scatter = Random.insideUnitCircle * dropScatterRadius;
            Vector3 spawnPos = transform.position + new Vector3(scatter.x, dropHeight, scatter.y);
            GameObject item = Instantiate(dropPrefab, spawnPos, Quaternion.identity);
            if(!item.TryGetComponent<NetworkIdentity>(out _))
            {
                Debug.LogWarning($"Dropped item ({item.name}) has no NetworkIdentity. It will only exist on server/host unless clients also instantiate it locally.");
            }
            else
            {
                Debug.Log($"Dropped item ({item.name}) has a NetworkIdentity. It will be synchronized across clients.");
            }
            NetworkServer.Spawn(item);
        }
    }

    void Deplete()
    {
        if (isDepleted) return;
        isDepleted = true;
    }

    void OnHealthSync(int _, int __) { }

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
        return isDepleted ? $"{nodeName}\nDepleted" : $"{nodeName}\nHP: {health}/{maxHealth}";
    }
}