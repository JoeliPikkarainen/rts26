using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
public class NetworkPlayer : NetworkBehaviour
{
    [Header("Sync")]
    [SerializeField] private float sendInterval = 0.05f;
    [SerializeField] private float interpolationSpeed = 12f;
    [SerializeField] private float minPositionDeltaToSend = 0.001f;
    [SerializeField] private float minRotationDeltaToSend = 0.1f;

    [SyncVar(hook = nameof(OnSyncedPositionChanged))] private Vector3 syncedPosition;
    [SyncVar(hook = nameof(OnSyncedRotationChanged))] private Quaternion syncedRotation;
    
    private PlayerHandler playerHandler;
    private Rigidbody rb;
    private float sendTimer;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;

    void Awake()
    {
        playerHandler = GetComponent<PlayerHandler>();
        rb = GetComponent<Rigidbody>();

        // Disable gameplay input by default; enable only on the local player instance.
        if (playerHandler != null)
        {
            playerHandler.enabled = false;
        }

        targetPosition = transform.position;
        targetRotation = transform.rotation;
        lastSentPosition = transform.position;
        lastSentRotation = transform.rotation;
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

        if (playerHandler != null)
        {
            playerHandler.RegisterBuildPrefabsOnClient();
        }

        ApplyLocalPlayerState(isLocalPlayer);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        ApplyLocalPlayerState(true);
    }

    public override void OnStopLocalPlayer()
    {
        ApplyLocalPlayerState(false);
        base.OnStopLocalPlayer();
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        sendTimer += Time.fixedDeltaTime;
        if (sendTimer < sendInterval)
        {
            return;
        }

        sendTimer = 0f;

        Vector3 currentPos = transform.position;
        Quaternion currentRot = transform.rotation;
        bool movedEnough = (currentPos - lastSentPosition).sqrMagnitude > minPositionDeltaToSend;
        bool rotatedEnough = Quaternion.Angle(currentRot, lastSentRotation) > minRotationDeltaToSend;

        if (!movedEnough && !rotatedEnough)
        {
            return;
        }

        lastSentPosition = currentPos;
        lastSentRotation = currentRot;
        CmdUpdatePlayerPosition(currentPos, currentRot);
    }

    void Update()
    {
        if (isLocalPlayer)
        {
            return;
        }
        
        float lerpT = Mathf.Clamp01(Time.deltaTime * interpolationSpeed);
        transform.position = Vector3.Lerp(transform.position, targetPosition, lerpT);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lerpT);
    }

    [Command]
    void CmdUpdatePlayerPosition(Vector3 pos, Quaternion rot)
    {
        syncedPosition = pos;
        syncedRotation = rot;
    }

    // -------------------------------------------------------------------------
    // World interaction commands (server-authoritative)
    // -------------------------------------------------------------------------

    /// <summary>Hit a networked gatherable (Tree, RockNode). Runs on server.</summary>
    [Command]
    public void CmdHitNetworkObject(NetworkIdentity target, int damage)
    {
        if (target == null) return;
        IGatherable gatherable = target.GetComponent<IGatherable>()
            ?? target.GetComponentInChildren<IGatherable>();
        if (gatherable != null)
        {
            gatherable.Gather(damage);
            return;
        }

        IDamageable damageable = target.GetComponent<IDamageable>()
            ?? target.GetComponentInChildren<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }
    }

    [Command]
    public void CmdRecruitNpc(NetworkIdentity npcIdentity)
    {
        if (npcIdentity == null) return;

        INpc npc = npcIdentity.GetComponent<INpc>()
            ?? npcIdentity.GetComponentInChildren<INpc>();
        npc?.Recruit(gameObject);
    }

    [Command]
    public void CmdSetNpcBehaviour(NetworkIdentity npcIdentity, NpcBehaviour behaviour)
    {
        if (npcIdentity == null) return;

        INpc npc = npcIdentity.GetComponent<INpc>()
            ?? npcIdentity.GetComponentInChildren<INpc>();
        npc?.SetBehaviour(behaviour);
    }

    [Command]
    public void CmdSetNpcGatherPreference(NetworkIdentity npcIdentity, GatherResourcePreference preference)
    {
        if (npcIdentity == null) return;

        INpc npc = npcIdentity.GetComponent<INpc>()
            ?? npcIdentity.GetComponentInChildren<INpc>();
        if (npc == null) return;

        npc.SetGatherPreference(preference);
        npc.SetBehaviour(NpcBehaviour.Gather);
    }

    /// <summary>Pick up a networked item. Server destroys it and tells this client to add it to inventory.</summary>
    [Command]
    public void CmdPickupItem(NetworkIdentity item)
    {
        if (item == null) return;
        IPickable pickable = item.GetComponent<IPickable>()
            ?? item.GetComponentInChildren<IPickable>();
        if (pickable == null) return;

        ItemData data = pickable.GetItemData();
        TargetOnItemPickedUp(data.itemType, data.quantity);
        NetworkServer.Destroy(item.gameObject);
    }

    [TargetRpc]
    void TargetOnItemPickedUp(string itemType, int qty)
    {
        Inventory inv = GetComponent<Inventory>();
        inv?.AddItem(new ItemData(itemType, qty, null));
    }

    /// <summary>Place a building. Server instantiates and spawns it for all clients.</summary>
    [Command]
    public void CmdPlaceBuilding(int buildIndex, Vector3 pos, Quaternion rot)
    {
        PlayerHandler ph = GetComponent<PlayerHandler>();
        if (ph == null) return;
        GameObject prefab = ph.GetBuildPrefab(buildIndex);
        if (prefab == null) return;
        GameObject placed = Instantiate(prefab, pos, rot);
        NetworkServer.Spawn(placed);
        IBuildable buildable = placed.GetComponent<IBuildable>()
            ?? placed.GetComponentInChildren<IBuildable>();
        buildable?.OnPlaced(gameObject);
    }

    private void OnSyncedPositionChanged(Vector3 oldValue, Vector3 newValue)
    {
        targetPosition = newValue;
    }

    private void OnSyncedRotationChanged(Quaternion oldValue, Quaternion newValue)
    {
        targetRotation = newValue;
    }

    private void ApplyLocalPlayerState(bool isControlledLocally)
    {
        if (playerHandler != null)
        {
            playerHandler.enabled = isControlledLocally;
            if (isControlledLocally)
            {
                playerHandler.SetNetworkPlayer(this);
            }
        }

        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].enabled = isControlledLocally;
        }

        AudioListener[] listeners = GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            listeners[i].enabled = isControlledLocally;
        }

        if (rb != null)
        {
            rb.isKinematic = !isControlledLocally;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
