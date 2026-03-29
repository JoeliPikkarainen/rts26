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
