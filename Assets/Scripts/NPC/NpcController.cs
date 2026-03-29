using Mirror;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Core NPC unit. Handles health, movement, state machine, and inventory.
/// Place on any goblin/unit prefab that needs to be recruitble by the player.
/// Requires a Rigidbody and a Collider on the same GameObject.
/// </summary>
[RequireComponent(typeof(Inventory))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkIdentity))]
public class NpcController : NetworkBehaviour, INpc, IDamageable, ITextInfoOverlay
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
    [SerializeField] private float actionSpeedMultiplier = 1f;
    [Header("Inventory")]
    [SerializeField] private int inventoryLimit = 6;
    [SerializeField] private float storageSearchRadius = 60f;
    [SerializeField] private float storageStopDistance = 1.25f;
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
    [SyncVar] private int currentHealth;
    [SyncVar(hook = nameof(OnOwnerNetIdChanged))] private uint ownerNetId;
    [SyncVar] private NpcBehaviour currentBehaviour;
    [SyncVar] private GatherResourcePreference gatherPreference = GatherResourcePreference.Closest;
    [SyncVar(hook = nameof(OnSyncedPositionChanged))] private Vector3 syncedPosition;
    [SyncVar(hook = nameof(OnSyncedRotationChanged))] private Quaternion syncedRotation;

    private GameObject owner;
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
    private StorageChestBuilding storageTarget;
    private Vector3 targetPosition;
    private Quaternion targetRotation;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    void Start()
    {
        currentHealth = maxHealth;
        currentBehaviour = startBehaviour;
        npcInventory = GetComponent<Inventory>();
        if (npcInventory != null)
        {
            npcInventory.SetMaxTotalItemCount(Mathf.Max(1, inventoryLimit));
        }
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        actionTimer = GetEffectiveActionCooldown();
        wanderOrigin = transform.position;
        wanderPauseTimer = Random.Range(wanderPauseDurationRange.x, wanderPauseDurationRange.y);

        if (startsHostile)
        {
            owner = null;
            currentBehaviour = NpcBehaviour.Attack;
        }

        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        syncedPosition = transform.position;
        syncedRotation = transform.rotation;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ResolveOwnerFromNetId();
        targetPosition = transform.position;
        targetRotation = transform.rotation;

        if (!isServer && rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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
        if (!isServer)
        {
            float lerpT = Mathf.Clamp01(Time.deltaTime * 12f);
            transform.position = Vector3.Lerp(transform.position, targetPosition, lerpT);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lerpT);
            return;
        }

        actionTimer -= Time.deltaTime;

        syncedPosition = transform.position;
        syncedRotation = transform.rotation;

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
        if (!isServer)
        {
            return;
        }

        if (!isRecruitable)
        {
            return;
        }

        owner = recruiter;
        ownerNetId = recruiter != null && recruiter.TryGetComponent(out NetworkIdentity recruiterIdentity) ? recruiterIdentity.netId : 0;
        gatherTarget = null;
        attackTarget = null;
        currentBehaviour = NpcBehaviour.Follow;
    }

    public void SetBehaviour(NpcBehaviour behaviour)
    {
        if (!isServer)
        {
            return;
        }

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
        if (!isServer)
        {
            return;
        }

        gatherTarget = target;
        currentBehaviour = NpcBehaviour.Gather;
    }

    public void SetGatherPreference(GatherResourcePreference preference)
    {
        if (!isServer)
        {
            return;
        }

        gatherPreference = preference;
        gatherTarget = null;
    }

    public GatherResourcePreference GetGatherPreference() => gatherPreference;

    public void SetActionCooldown(float cooldownSeconds)
    {
        actionCooldown = Mathf.Max(0.05f, cooldownSeconds);
    }

    public void SetActionSpeedMultiplier(float multiplier)
    {
        actionSpeedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void ModifyActionSpeedMultiplier(float delta)
    {
        SetActionSpeedMultiplier(actionSpeedMultiplier + delta);
    }

    public void AttackTarget(GameObject target)
    {
        if (!isServer)
        {
            return;
        }

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
        if (!isServer)
        {
            return;
        }

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
        if (!isServer)
        {
            return;
        }

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
        if (!isServer)
        {
            return;
        }

        NetworkServer.Destroy(gameObject);
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
        if (!isServer)
        {
            return;
        }

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
        if (npcInventory != null && npcInventory.IsFull())
        {
            UpdateStorageDeposit();
            return;
        }

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
        actionTimer = GetEffectiveActionCooldown();

        // Server-authoritative gather action.
        currentGatherable?.Gather(attackDamage);
        TryPickupNearbyItem();
    }

    void UpdateStorageDeposit()
    {
        if (storageTarget == null || !storageTarget.gameObject.activeInHierarchy)
        {
            storageTarget = FindNearestStorageChest();
            if (storageTarget == null)
            {
                return;
            }
        }

        float distanceToStorage = DistanceToTargetSurfaceXZ(storageTarget.gameObject);
        float stopDistance = Mathf.Max(0.2f, storageStopDistance);
        if (distanceToStorage > stopDistance)
        {
            MoveToward(storageTarget.transform.position, stopDistance);
            return;
        }

        if (actionTimer > 0f)
        {
            return;
        }

        actionTimer = GetEffectiveActionCooldown();

        bool depositedAny = storageTarget.DepositAllFrom(npcInventory);
        if (!depositedAny)
        {
            // Chest cannot currently accept items, try another one next cycle.
            storageTarget = null;
            return;
        }

        storageTarget = null;
        gatherTarget = null;
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
            if (itemData == null || npcInventory == null || !npcInventory.CanAddItem(itemData))
            {
                continue;
            }

            GameObject itemObject = nearby[i].gameObject;
            npcInventory.AddItem(itemData);

            NetworkIdentity itemIdentity = itemObject.GetComponent<NetworkIdentity>()
                ?? itemObject.GetComponentInParent<NetworkIdentity>();
            if (itemIdentity != null && itemIdentity.netId != 0)
            {
                NetworkServer.Destroy(itemIdentity.gameObject);
            }
            else
            {
                pickable.OnPickup();
            }

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

    StorageChestBuilding FindNearestStorageChest()
    {
        StorageChestBuilding[] allStorageChests = FindObjectsByType<StorageChestBuilding>();
        StorageChestBuilding nearest = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < allStorageChests.Length; i++)
        {
            if (allStorageChests[i] == null || !allStorageChests[i].gameObject.activeInHierarchy)
            {
                continue;
            }

            Inventory storageInventory = allStorageChests[i].GetStorageInventory();
            if (storageInventory == null)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, allStorageChests[i].transform.position);
            if (distance > storageSearchRadius)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = allStorageChests[i];
            }
        }

        return nearest;
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
        actionTimer = GetEffectiveActionCooldown();

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

    float GetEffectiveActionCooldown()
    {
        return actionCooldown / Mathf.Max(0.1f, actionSpeedMultiplier);
    }

    void OnOwnerNetIdChanged(uint oldValue, uint newValue)
    {
        ResolveOwnerFromNetId();
    }

    void ResolveOwnerFromNetId()
    {
        owner = null;
        if (ownerNetId == 0)
        {
            return;
        }

        if (NetworkClient.spawned.TryGetValue(ownerNetId, out NetworkIdentity ownerIdentity))
        {
            owner = ownerIdentity.gameObject;
        }
        else if (NetworkServer.spawned.TryGetValue(ownerNetId, out NetworkIdentity serverIdentity))
        {
            owner = serverIdentity.gameObject;
        }
    }

    void OnSyncedPositionChanged(Vector3 oldValue, Vector3 newValue)
    {
        targetPosition = newValue;
    }

    void OnSyncedRotationChanged(Quaternion oldValue, Quaternion newValue)
    {
        targetRotation = newValue;
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
