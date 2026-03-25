using UnityEngine;

public class GoblinSpawner : MonoBehaviour
{
    [SerializeField] private GameObject goblinPrefab;
    [SerializeField] private BoxCollider groundCollider;
    [SerializeField] private int timeSpawnMs;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Vector3 startPos = RandomPoint(groundCollider.bounds);
        GameObject goblin = Instantiate(goblinPrefab, startPos, Quaternion.identity);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public static Vector3 RandomPoint(Bounds bounds)
    {
        return new Vector3(
        Random.Range(bounds.min.x,bounds.max.x),
        1f,
        Random.Range(bounds.min.z,bounds.max.z)
        );
    }
}
