using Mirror;
using UnityEngine;

public class GameNetworkManager : NetworkManager
{
    [Header("Lobby")]
    [SerializeField] private bool autoStartWhenHostConnects;

    private bool matchStarted;

    public override void Awake()
    {
        // Lobby flow: clients connect first, player objects spawn only when host starts the match.
        autoCreatePlayer = false;
        base.Awake();
    }

    
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"Client connected: {conn.connectionId}");

        if (autoStartWhenHostConnects && NetworkServer.connections.Count == 1)
        {
            StartMatch();
        }
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (!matchStarted)
        {
            Debug.Log($"Lobby: ignored AddPlayer for conn {conn.connectionId}. Waiting for host to start match.");
            return;
        }

        SpawnPlayerForConnection(conn);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"Client disconnected: {conn.connectionId}");
        base.OnServerDisconnect(conn);
    }

    public override void OnClientConnect()
    {
        Debug.Log("Connected to server");
        base.OnClientConnect();
    }

    public override void OnClientDisconnect()
    {
        Debug.Log("Disconnected from server");
        base.OnClientDisconnect();
    }

    [ContextMenu("Start Match")]
    public void StartMatch()
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("StartMatch can only run on the active server/host.");
            return;
        }

        if (matchStarted)
        {
            return;
        }

        matchStarted = true;

        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
        {
            if (connection == null || connection.identity != null)
            {
                continue;
            }

            SpawnPlayerForConnection(connection);
        }

        Debug.Log("Match started: spawned all connected players.");
    }

    private void SpawnPlayerForConnection(NetworkConnectionToClient conn)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned on GameNetworkManager.");
            return;
        }

        if (!playerPrefab.TryGetComponent(out NetworkIdentity _))
        {
            Debug.LogError("Player prefab must have a NetworkIdentity component on the prefab root.");
            return;
        }

        Transform spawnPoint = GetStartPosition();
        GameObject player = spawnPoint != null
            ? Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation)
            : Instantiate(playerPrefab);

        player.name = $"{playerPrefab.name} [connId={conn.connectionId}]";
        NetworkServer.AddPlayerForConnection(conn, player);
        Debug.Log($"Player spawned for connection {conn.connectionId}");
    }
}
