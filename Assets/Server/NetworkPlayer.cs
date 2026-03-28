using Mirror;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    [SyncVar] public Vector3 syncedPosition;
    [SyncVar] public Quaternion syncedRotation;
    
    private PlayerHandler playerHandler;
    private Rigidbody rb;
    private float sendRateTimer;
    private float sendRate = 0.1f; // Send updates 10 times per second

    void Start()
    {
        playerHandler = GetComponent<PlayerHandler>();
        rb = GetComponent<Rigidbody>();
        
        if (!isLocalPlayer)
        {
            // Disable input for remote players
            if (playerHandler != null)
                playerHandler.enabled = false;
        }
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        sendRateTimer += Time.fixedDeltaTime;
        if (sendRateTimer >= sendRate)
        {
            CmdUpdatePlayerPosition(transform.position, transform.rotation);
            sendRateTimer = 0f;
        }
    }

    void Update()
    {
        if (isLocalPlayer) return;
        
        // Smoothly move remote players to synced position
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero; // Stop physics for remote players
            transform.position = Vector3.Lerp(transform.position, syncedPosition, Time.deltaTime * 5f);
            transform.rotation = Quaternion.Lerp(transform.rotation, syncedRotation, Time.deltaTime * 5f);
        }
    }

    [Command]
    void CmdUpdatePlayerPosition(Vector3 pos, Quaternion rot)
    {
        syncedPosition = pos;
        syncedRotation = rot;
    }
}
