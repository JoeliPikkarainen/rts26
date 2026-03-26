using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Core NPC unit. Handles health, movement, state machine, and inventory.
/// Place on any goblin/unit prefab that needs to be recruitble by the player.
/// Requires a Rigidbody and a Collider on the same GameObject.
/// </summary>
[RequireComponent(typeof(Inventory))]
[RequireComponent(typeof(Rigidbody))]
public class NpcController : MonoBehaviour, INpc, IDamageable, ITextInfoOverlay
{
    [SerializeField] private string npcId = "goblin";
    [SerializeField] private string displayName = "Goblin";

    [Header("Stats")]
    [SerializeField] private int maxHealth = 30;
    [SerializeField] private int attackDamage = 5;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackStopDistance = 1.0f;
    [SerializeField] private float attackSearchRadius = 14f;
    [SerializeField] private float attackRetargetInterval = 0.25f;
    [SerializeField] private LayerMask attackTargetMask = ~0;
    [SerializeField] private float gatherRange = 2f;
    [SerializeField] private float gatherStopDistance = 0.9f;
    [SerializeField] private float gatherSearchRadius = 40f;
    [SerializeField] private float pickupRange = 2.2f;
    [SerializeField] private LayerMask pickupMask = ~0;
    [SerializeField] private float actionCooldown = 1.5f;
    [Header("Wander")]
    [SerializeField] private float wanderRadius = 7f;
    [SerializeField] private float wanderArriveDistance = 0.5f;
    [SerializeField] private Vector2 wanderPauseDurationRange = new Vector2(0.8f, 2.0f);

    [Header("State")]
    [SerializeField] private NpcBehaviour startBehaviour = NpcBehaviour.Wander;
    [SerializeField] private bool isRecruitable = true;
    [SerializeField] private bool startsHostile = false;
    [SerializeField] private bool enableVerboseLogs = false;

    // Runtime state
    private int currentHealth;
    private GameObject owner;           // null = neutral
    private NpcBehaviour currentBehaviour;
    private GatherResourcePreference gatherPreference = GatherResourcePreference.Closest;
    private GameObject gatherTarget;
    private GameObject attackTarget;
    private Inventory npcInventory;
    private Rigidbody rb;
    private float actionTimer;
    private float gatherRetargetTimer;
    private float attackRetargetTimer;
    private Vector3 wanderOrigin;
    private Vector3 wanderTarget;
    private bool hasWanderTarget;
    private float wanderPauseTimer;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    void Start()
    {
        currentHealth = maxHealth;
        currentBehaviour = startBehaviour;
        npcInventory = GetComponent<Inventory>();
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        actionTimer = actionCooldown;
        wanderOrigin = transform.position;
        wanderPauseTimer = Random.Range(wanderPauseDurationRange.x, wanderPauseDurationRange.y);

        if (startsHostile)
        {
            owner = null;
            currentBehaviour = NpcBehaviour.Attack;
        }
    }

    void OnEnable()
    {
        GameEvents.OnHit += HandleHit;
    }

    void OnDisable()
    {
        GameEvents.OnHit -= HandleHit;
    }

    void Update()
    {
        actionTimer -= Time.deltaTime;

        switch (currentBehaviour)
        {
            case NpcBehaviour.Idle:    break;
            case NpcBehaviour.Follow:  UpdateFollow();  break;
            case NpcBehaviour.Gather:  UpdateGather();  break;
            case NpcBehaviour.Attack:  UpdateAttack();  break;
            case NpcBehaviour.Defend:  UpdateDefend();  break;
            case NpcBehaviour.Wander:  UpdateWander();  break;
        }
    }

    // -------------------------------------------------------------------------
    // INpc
    // -------------------------------------------------------------------------

    public string GetNpcId() => npcId;
    public string GetDisplayName() => displayName;

    public void Recruit(GameObject recruiter)
    {
        if (!isRecruitable)
        {
            return;
        }

        owner = recruiter;
        gatherTarget = null;
        attackTarget = null;
        currentBehaviour = NpcBehaviour.Follow;
    }

    public void SetBehaviour(NpcBehaviour behaviour)
    {
        currentBehaviour = behaviour;
        if (behaviour != NpcBehaviour.Gather) gatherTarget = null;
        if (behaviour != NpcBehaviour.Attack) attackTarget = null;
        if (behaviour == NpcBehaviour.Wander)
        {
            wanderOrigin = transform.position;
            hasWanderTarget = false;
            wanderPauseTimer = Random.Range(wanderPauseDurationRange.x, wanderPauseDurationRange.y);
        }
    }

    public NpcBehaviour GetCurrentBehaviour() => currentBehaviour;

    public void GatherFrom(GameObject target)
    {
        gatherTarget = target;
        currentBehaviour = NpcBehaviour.Gather;
    }

    public void SetGatherPreference(GatherResourcePreference preference)
    {
        gatherPreference = preference;
        gatherTarget = null;
    }

    public GatherResourcePreference GetGatherPreference() => gatherPreference;

    public void AttackTarget(GameObject target)
    {
        attackTarget = target;
        currentBehaviour = NpcBehaviour.Attack;
    }

    public GameObject GetOwner() => owner;
    public bool CanBeRecruited() => isRecruitable;
    public bool IsNeutral() => owner == null;

    public bool IsHostileTo(GameObject other)
    {
        if (other == null || other == gameObject)
        {
            return false;
        }

        bool isHostileFaction = startsHostile && owner == null;

        // Non-recruitable hostile NPCs attack the player and player-owned units.
        if (isHostileFaction)
        {
            if (other.CompareTag("Player"))
            {
                return true;
            }

            NpcController otherNpc = GetNpcController(other);
            if (otherNpc != null && otherNpc.GetOwner() != null)
            {
                return true;
            }
        }

        // Recruited NPCs attack hostile NPCs.
        if (owner != null)
        {
            NpcController otherNpc = GetNpcController(other);
            if (otherNpc != null && otherNpc.startsHostile && otherNpc.owner == null)
            {
                return true;
            }
        }

        return false;
    }

    // -------------------------------------------------------------------------
    // IDamageable
    // -------------------------------------------------------------------------

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (enableVerboseLogs)
        {
            Debug.Log($"{displayName} took {amount} damage. HP: {currentHealth}/{maxHealth}");
        }
        if (currentHealth <= 0)
            Die();
    }

    void HandleHit(HitEvent hit)
    {
        if (hit.dst == gameObject || (hit.dst != null && hit.dst.transform.IsChildOf(transform)))
        {
            TakeDamage(hit.ctx.dmg);
        }
    }

    // -------------------------------------------------------------------------
    // ITextInfoOverlay
    // -------------------------------------------------------------------------

    public string GetInfoText()
    {
        string ownerName = owner != null ? owner.name : "Neutral";
        string carrying  = BuildInventorySummary();
        string recruitText = isRecruitable ? "Recruitable" : "Hostile";
        string gatherText = currentBehaviour == NpcBehaviour.Gather ? $"\nGather: {gatherPreference}" : string.Empty;
        return $"{displayName}\nHP: {currentHealth}/{maxHealth}\nOwner: {ownerName}\nType: {recruitText}\nBehaviour: {currentBehaviour}{gatherText}\nCarrying: {carrying}";
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    void Die()
    {
        Destroy(gameObject);
    }

    string BuildInventorySummary()
    {
        if (npcInventory == null) return "—";
        List<ItemData> allItems = npcInventory.GetAllItems();
        if (allItems.Count == 0) return "Empty";
        return string.Join(", ", allItems.ConvertAll(i => i.ToString()));
    }

    void MoveToward(Vector3 targetPos, float stoppingDistance)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.magnitude <= stoppingDistance) return;

        dir.Normalize();
        rb.MovePosition(rb.position + dir * moveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(dir);
    }

    // -------------------------------------------------------------------------
    // Behaviour updates
    // -------------------------------------------------------------------------

    void UpdateFollow()
    {
        if (owner == null) { currentBehaviour = NpcBehaviour.Idle; return; }
        MoveToward(owner.transform.position, 2f);
    }

    void UpdateGather()
    {
        gatherRetargetTimer -= Time.deltaTime;

        if (gatherTarget == null || !gatherTarget.activeInHierarchy)
        {
            gatherTarget = FindNearestGatherableTarget();
            if (gatherTarget == null)
            {
                TryPickupNearbyItem();
                return;
            }
        }

        IGatherable currentGatherable = gatherTarget.GetComponent<IGatherable>() ?? gatherTarget.GetComponentInParent<IGatherable>();
        if (currentGatherable == null)
        {
            gatherTarget = FindNearestGatherableTarget();
            if (gatherTarget == null)
            {
                TryPickupNearbyItem();
                return;
            }
        }

        if (gatherRetargetTimer <= 0f)
        {
            GameObject betterTarget = FindNearestGatherableTarget();
            if (betterTarget != null)
            {
                gatherTarget = betterTarget;
            }
            gatherRetargetTimer = 1.0f;
        }

        float distToSurface = DistanceToTargetSurfaceXZ(gatherTarget);
        float stopDistance = Mathf.Clamp(gatherStopDistance, 0.2f, gatherRange);
        if (distToSurface > stopDistance)
        {
            MoveToward(gatherTarget.transform.position, stopDistance);
            TryPickupNearbyItem();
            return;
        }

        if (actionTimer > 0f)
        {
            TryPickupNearbyItem();
            return;
        }
        actionTimer = actionCooldown;

        // Match player-style interaction: gathering deals damage through the same hit event path.
        GameEvents.OnHit?.Invoke(new HitEvent(gameObject, gatherTarget, new HitEvent.HitCtx { dmg = attackDamage }));
        TryPickupNearbyItem();
    }

    float DistanceToTargetSurfaceXZ(GameObject target)
    {
        if (target == null)
        {
            return float.MaxValue;
        }

        Vector3 from = transform.position;
        Collider[] targetColliders = target.GetComponentsInChildren<Collider>();
        float best = float.MaxValue;

        if (targetColliders.Length == 0)
        {
            Vector3 toTarget = target.transform.position - from;
            toTarget.y = 0f;
            return toTarget.magnitude;
        }

        for (int i = 0; i < targetColliders.Length; i++)
        {
            Vector3 closest = targetColliders[i].ClosestPoint(from);
            Vector3 delta = closest - from;
            delta.y = 0f;
            float d = delta.magnitude;
            if (d < best)
            {
                best = d;
            }
        }

        return best;
    }

    void TryPickupNearbyItem()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, pickupRange, pickupMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < nearby.Length; i++)
        {
            IPickable pickable = nearby[i].GetComponent<IPickable>();
            if (pickable == null)
            {
                pickable = nearby[i].GetComponentInParent<IPickable>();
            }

            if (pickable == null)
            {
                continue;
            }

            ItemData itemData = pickable.GetItemData();
            GameObject itemObject = nearby[i].gameObject;
            GameEvents.OnPickup?.Invoke(new PickupEvent(gameObject, itemObject, itemData));
            pickable.OnPickup();
            if (enableVerboseLogs)
            {
                Debug.Log($"{displayName} picked up {itemData.itemType}");
            }
            return;
        }
    }

    GameObject FindNearestGatherableTarget()
    {
        MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>();
        GameObject nearestTarget = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < allBehaviours.Length; i++)
        {
            if (allBehaviours[i] is not IGatherable)
            {
                continue;
            }

            if (!MatchesGatherPreference(allBehaviours[i].gameObject))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, allBehaviours[i].transform.position);
            if (distance > gatherSearchRadius)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearestTarget = allBehaviours[i].gameObject;
            }
        }

        return nearestTarget;
    }

    bool MatchesGatherPreference(GameObject candidate)
    {
        if (candidate == null || gatherPreference == GatherResourcePreference.Closest)
        {
            return candidate != null;
        }

        if (gatherPreference == GatherResourcePreference.Tree)
        {
            return candidate.GetComponent<Tree>() != null || candidate.GetComponentInParent<Tree>() != null;
        }

        if (gatherPreference == GatherResourcePreference.Rock)
        {
            return candidate.GetComponent<RockNode>() != null || candidate.GetComponentInParent<RockNode>() != null;
        }

        return true;
    }

    void UpdateAttack()
    {
        attackRetargetTimer -= Time.deltaTime;

        if (attackTarget == null || !attackTarget.activeInHierarchy || !IsHostileTo(attackTarget))
        {
            if (attackRetargetTimer <= 0f)
            {
                attackTarget = FindNearestAttackTarget();
                attackRetargetTimer = Mathf.Max(0.05f, attackRetargetInterval);
            }

            if (attackTarget == null)
            {
                currentBehaviour = owner != null ? NpcBehaviour.Follow : (startsHostile ? NpcBehaviour.Attack : NpcBehaviour.Idle);
                return;
            }
        }

        float distToSurface = DistanceToTargetSurfaceXZ(attackTarget);
        float stopDistance = Mathf.Clamp(attackStopDistance, 0.2f, attackRange);
        if (distToSurface > stopDistance)
        {
            MoveToward(attackTarget.transform.position, stopDistance);
            return;
        }

        if (actionTimer > 0f) return;
        actionTimer = actionCooldown;

        IDamageable damageable = attackTarget.GetComponent<IDamageable>() ?? attackTarget.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(attackDamage);
        }
        else
        {
            GameEvents.OnHit?.Invoke(new HitEvent(gameObject, attackTarget, new HitEvent.HitCtx { dmg = attackDamage }));
        }
    }

    GameObject FindNearestAttackTarget()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, attackSearchRadius, attackTargetMask, QueryTriggerInteraction.Ignore);
        GameObject nearest = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < nearby.Length; i++)
        {
            GameObject candidate = nearby[i].transform.root.gameObject;
            if (!IsHostileTo(candidate))
            {
                continue;
            }

            Vector3 delta = candidate.transform.position - transform.position;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                nearest = candidate;
            }
        }

        return nearest;
    }

    NpcController GetNpcController(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        NpcController npc = target.GetComponent<NpcController>();
        if (npc != null)
        {
            return npc;
        }

        return target.GetComponentInParent<NpcController>();
    }

    void UpdateDefend()
    {
        if (owner == null) { currentBehaviour = NpcBehaviour.Idle; return; }
        // Stay close to owner; future expansion: scan for nearby threats and switch to Attack
        MoveToward(owner.transform.position, 3f);
    }

    void UpdateWander()
    {
        if (owner != null && currentBehaviour == NpcBehaviour.Wander)
        {
            // Commanded wander after recruitment: roam around current local area.
            wanderOrigin = transform.position;
        }

        if (wanderPauseTimer > 0f)
        {
            wanderPauseTimer -= Time.deltaTime;
            return;
        }

        if (!hasWanderTarget)
        {
            Vector2 offset2D = Random.insideUnitCircle * Mathf.Max(0.5f, wanderRadius);
            wanderTarget = wanderOrigin + new Vector3(offset2D.x, 0f, offset2D.y);
            wanderTarget.y = transform.position.y;
            hasWanderTarget = true;
        }

        MoveToward(wanderTarget, Mathf.Max(0.1f, wanderArriveDistance));

        Vector3 toTarget = wanderTarget - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude <= Mathf.Max(0.1f, wanderArriveDistance))
        {
            hasWanderTarget = false;
            wanderPauseTimer = Random.Range(wanderPauseDurationRange.x, wanderPauseDurationRange.y);
        }
    }
}
