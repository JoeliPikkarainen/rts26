using Mirror;
using UnityEngine;

/// <summary>
/// Place this on a GameObject with a NetworkIdentity in MultiPlayerLobbyScene.
/// The server owns the list; Mirror automatically syncs it to every connected client.
/// </summary>
public class LobbyPlayerList : NetworkBehaviour
{
    public static LobbyPlayerList Instance { get; private set; }

    // Server writes to this; all clients receive changes automatically.
    public readonly SyncList<string> playerNames = new SyncList<string>();

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
