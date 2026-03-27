using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldCreator : MonoBehaviour
{
    [Serializable]
    public class GroundSettings
    {
        public bool generateGround = true;
        public Vector2 size = new Vector2(120f, 120f);
        public float thickness = 2f;
        public Material groundMaterial;
        public bool createCollider = true;
    }

    [Serializable]
    public class SpawnEntry
    {
        public string id = "entry";
        public GameObject prefab;
        [Min(0)] public int minCount = 5;
        [Min(0)] public int maxCount = 20;
        [Min(0f)] public float occurrenceWeight = 1f;
        public Vector2 uniformScaleRange = Vector2.one;
        public bool randomYRotation = true;
    }

    [Serializable]
    public class SpawnCategory
    {
        public string categoryName = "Category";
        [Min(0f)] public float minSpacing = 2f;
        [Min(1)] public int maxPlacementAttemptsPerObject = 20;
        public List<SpawnEntry> entries = new List<SpawnEntry>();
    }

    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool clearPreviousGenerated = true;
    [SerializeField] private bool deterministic = true;
    [SerializeField] private int seed = 12345;

    [Header("Ground")]
    [SerializeField] private GroundSettings ground = new GroundSettings();

    [Header("Spawn Categories")]
    [SerializeField] private SpawnCategory environmentCategory = new SpawnCategory { categoryName = "Environment", minSpacing = 3f };
    [SerializeField] private SpawnCategory spawnerCategory = new SpawnCategory { categoryName = "Spawners", minSpacing = 6f };
    [SerializeField] private SpawnCategory npcCategory = new SpawnCategory { categoryName = "NPCs", minSpacing = 3f };

    [Header("Placement")]
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField] private float raycastHeight = 200f;
    [SerializeField] private float spawnYOffset = 0f;
    [SerializeField] private Vector2 borderPadding = new Vector2(2f, 2f);

    private const string GeneratedRootName = "__GeneratedWorld";
    private const string GeneratedGroundName = "__GeneratedGround";

    void Start()
    {
        if (generateOnStart)
        {
            GenerateWorld();
        }
    }

    [ContextMenu("Generate World")]
    public void GenerateWorld()
    {
        if (clearPreviousGenerated)
        {
            ClearGeneratedWorld();
        }

        Transform generatedRoot = GetOrCreateGeneratedRoot();

        if (ground.generateGround)
        {
            CreateGround(generatedRoot);
        }

        System.Random rng = deterministic ? new System.Random(seed) : new System.Random(Guid.NewGuid().GetHashCode());

        SpawnCategoryEntries(environmentCategory, generatedRoot, rng);
        SpawnCategoryEntries(spawnerCategory, generatedRoot, rng);
        SpawnCategoryEntries(npcCategory, generatedRoot, rng);
    }

    [ContextMenu("Clear Generated World")]
    public void ClearGeneratedWorld()
    {
        Transform existing = transform.Find(GeneratedRootName);
        if (existing == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(existing.gameObject);
        }
        else
        {
            DestroyImmediate(existing.gameObject);
        }
    }

    Transform GetOrCreateGeneratedRoot()
    {
        Transform existing = transform.Find(GeneratedRootName);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject(GeneratedRootName);
        root.transform.SetParent(transform, false);
        return root.transform;
    }

    void CreateGround(Transform parent)
    {
        GameObject groundObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        groundObj.name = GeneratedGroundName;
        groundObj.transform.SetParent(parent, false);

        float width = Mathf.Max(1f, ground.size.x);
        float depth = Mathf.Max(1f, ground.size.y);
        float thickness = Mathf.Max(0.2f, ground.thickness);

        groundObj.transform.localScale = new Vector3(width, thickness, depth);
        groundObj.transform.position = transform.position + Vector3.down * (thickness * 0.5f);

        if (!ground.createCollider)
        {
            Collider col = groundObj.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(col);
                }
                else
                {
                    DestroyImmediate(col);
                }
            }
        }

        if (ground.groundMaterial != null)
        {
            Renderer renderer = groundObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = ground.groundMaterial;
            }
        }
    }

    void SpawnCategoryEntries(SpawnCategory category, Transform root, System.Random rng)
    {
        if (category == null || category.entries == null || category.entries.Count == 0)
        {
            return;
        }

        List<Vector3> placedPositions = new List<Vector3>();
        Transform categoryRoot = new GameObject(category.categoryName).transform;
        categoryRoot.SetParent(root, false);

        for (int i = 0; i < category.entries.Count; i++)
        {
            SpawnEntry entry = category.entries[i];
            if (entry == null || entry.prefab == null)
            {
                continue;
            }

            int count = GetCountForEntry(entry, rng);
            for (int n = 0; n < count; n++)
            {
                bool placed = TryPlaceEntry(entry, categoryRoot, rng, category.minSpacing, category.maxPlacementAttemptsPerObject, placedPositions);
                if (!placed)
                {
                    break;
                }
            }
        }
    }

    int GetCountForEntry(SpawnEntry entry, System.Random rng)
    {
        int min = Mathf.Max(0, entry.minCount);
        int max = Mathf.Max(min, entry.maxCount);
        if (max == min)
        {
            return min;
        }

        float weightedRoll = (float)rng.NextDouble();
        float normalizedWeight = Mathf.Clamp01(entry.occurrenceWeight / (entry.occurrenceWeight + 1f));
        float t = Mathf.Clamp01((weightedRoll + normalizedWeight) * 0.5f + normalizedWeight * 0.5f);
        return Mathf.RoundToInt(Mathf.Lerp(min, max, t));
    }

    bool TryPlaceEntry(SpawnEntry entry, Transform parent, System.Random rng, float minSpacing, int maxAttempts, List<Vector3> placedPositions)
    {
        int attempts = Mathf.Max(1, maxAttempts);
        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidate = GetRandomGroundPoint(rng);
            if (!IsFarEnough(candidate, minSpacing, placedPositions))
            {
                continue;
            }

            Quaternion rot = entry.randomYRotation
                ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f)
                : Quaternion.identity;

            GameObject instance = Instantiate(entry.prefab, candidate, rot, parent);
            instance.name = entry.prefab.name;

            float minScale = Mathf.Max(0.1f, entry.uniformScaleRange.x);
            float maxScale = Mathf.Max(minScale, entry.uniformScaleRange.y);
            float scale = Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
            instance.transform.localScale *= scale;

            placedPositions.Add(candidate);
            return true;
        }

        return false;
    }

    Vector3 GetRandomGroundPoint(System.Random rng)
    {
        float width = Mathf.Max(1f, ground.size.x);
        float depth = Mathf.Max(1f, ground.size.y);

        float halfWidth = width * 0.5f - borderPadding.x;
        float halfDepth = depth * 0.5f - borderPadding.y;

        halfWidth = Mathf.Max(0.5f, halfWidth);
        halfDepth = Mathf.Max(0.5f, halfDepth);

        float x = Mathf.Lerp(-halfWidth, halfWidth, (float)rng.NextDouble());
        float z = Mathf.Lerp(-halfDepth, halfDepth, (float)rng.NextDouble());

        Vector3 origin = transform.position + new Vector3(x, raycastHeight, z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, placementMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * spawnYOffset;
        }

        return transform.position + new Vector3(x, spawnYOffset, z);
    }

    bool IsFarEnough(Vector3 candidate, float minSpacing, List<Vector3> placed)
    {
        if (placed == null || placed.Count == 0 || minSpacing <= 0f)
        {
            return true;
        }

        float sqrMin = minSpacing * minSpacing;
        for (int i = 0; i < placed.Count; i++)
        {
            if ((candidate - placed[i]).sqrMagnitude < sqrMin)
            {
                return false;
            }
        }

        return true;
    }
}
