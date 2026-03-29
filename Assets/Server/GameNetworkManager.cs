using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mirror;
using Unity.VisualScripting;
using UnityEngine;

public struct LobbyPlayersSnapshotMessage : NetworkMessage
{
    public string[] playerNames;
}

public class GameNetworkManager : NetworkManager
{
    [Header("Scenes")]
    [Scene] [SerializeField] private string multiplayerLobbyScene = "MultiPlayerLobbyScene";
    [Scene] [SerializeField] private string multiplayerGameScene = "GeneratorScene";

    [Header("Lobby")]
    [SerializeField] private bool autoStartWhenHostConnects;

    private bool matchStarted;

    [Header("Room")]
    [SerializeField] private NetworkRoomPlayer roomPlayerPrefab = null;

    [UnitHeaderInspectable("Game")]
    [SerializeField] private NetworkPlayer gamePlayerPrefab = null;

    private readonly Dictionary<int, string> connectedLobbyPlayers = new Dictionary<int, string>();
    private bool pendingGameSceneStart;

    public static event Action<IReadOnlyList<string>> LobbyPlayersUpdated;

    private static readonly List<string> latestLobbyPlayers = new List<string>();
    public static IReadOnlyList<string> LatestLobbyPlayers => latestLobbyPlayers;

    public override void Awake()
    {
        // Lobby flow: clients connect first, player objects spawn only when host starts the match.
        autoCreatePlayer = false;

        // Keep Mirror's built-in player prefab in sync so runtime player spawns are always registered on clients.
        if (gamePlayerPrefab != null)
        {
            playerPrefab = gamePlayerPrefab.gameObject;
        }

        // Mirror uses onlineScene when host/server starts; connected clients are instructed to load it.
        string lobbySceneName = GetSceneName(multiplayerLobbyScene);
        if (!string.IsNullOrWhiteSpace(lobbySceneName))
        {
            onlineScene = lobbySceneName;
        }
        else
        {
            Debug.LogWarning("multiplayerLobbyScene is empty. Assign your lobby scene name (e.g. MultiPlayerLobbyScene).", this);
        }

        base.Awake();
    }

    
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"Client connected: {conn.connectionId}");

        string playerLabel = conn is LocalConnectionToClient ? "Host" : $"Player {conn.connectionId}";
        connectedLobbyPlayers[conn.connectionId] = playerLabel;
        BroadcastLobbyPlayers();

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

    public override void OnServerReady(NetworkConnectionToClient conn)
    {
        base.OnServerReady(conn);

        // Clients become ready after loading the gameplay scene.
        // If match already started, spawn their player once ready.
        if (matchStarted && conn != null && conn.identity == null)
        {
            SpawnPlayerForConnection(conn);
        }
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"Client disconnected: {conn.connectionId}");

        connectedLobbyPlayers.Remove(conn.connectionId);
        BroadcastLobbyPlayers();

        base.OnServerDisconnect(conn);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        NetworkClient.RegisterHandler<LobbyPlayersSnapshotMessage>(OnLobbyPlayersSnapshotMessage);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        NetworkClient.UnregisterHandler<LobbyPlayersSnapshotMessage>();
        ApplyLobbyPlayersOnClient(Array.Empty<string>());
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

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();

        if (!string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, GetSceneName(multiplayerGameScene), StringComparison.Ordinal))
        {
            return;
        }

        WorldCreator worldCreator = FindObjectOfType<WorldCreator>();
        if (worldCreator != null)
        {
            worldCreator.GenerateWorld();
        }
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
        SpawnMissingPlayers();
        Debug.Log("Match started: spawned all connected players.");
    }

    [ContextMenu("Start Game From Lobby")]
    public void StartGameFromLobby()
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("StartGameFromLobby can only run on the active server/host.");
            return;
        }

        string gameSceneName = GetSceneName(multiplayerGameScene);
        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError("multiplayerGameScene is empty. Assign your gameplay scene on GameNetworkManager.");
            return;
        }

        pendingGameSceneStart = true;
        ServerChangeScene(gameSceneName);
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        if (!pendingGameSceneStart || !string.Equals(sceneName, GetSceneName(multiplayerGameScene), StringComparison.Ordinal))
        {
            return;
        }

        pendingGameSceneStart = false;
        PrepareGameplayWorld();
        StartMatch();
    }

    private void PrepareGameplayWorld()
    {
        WorldCreator worldCreator = FindObjectOfType<WorldCreator>();
        if (worldCreator == null)
        {
            Debug.LogWarning("No WorldCreator found in gameplay scene. Players will spawn using existing start positions.");
            return;
        }

        worldCreator.GenerateWorld();
    }

    private void SpawnMissingPlayers()
    {
        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
        {
            if (connection == null || connection.identity != null)
            {
                continue;
            }

            if (!connection.isReady)
            {
                continue;
            }

            SpawnPlayerForConnection(connection);
        }
    }

    private void SpawnPlayerForConnection(NetworkConnectionToClient conn)
    {
        GameObject prefabToSpawn = gamePlayerPrefab != null ? gamePlayerPrefab.gameObject : playerPrefab;

        if (prefabToSpawn == null)
        {
            Debug.LogError("No player prefab is assigned. Set gamePlayerPrefab or playerPrefab on GameNetworkManager.");
            return;
        }

        if (!prefabToSpawn.TryGetComponent(out NetworkIdentity _))
        {
            Debug.LogError("Spawn player prefab must have a NetworkIdentity component on the prefab root.");
            return;
        }

        Transform spawnPoint = GetStartPosition();
        GameObject player = spawnPoint != null
            ? Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation)
            : Instantiate(prefabToSpawn);

        player.name = $"{prefabToSpawn.name} [connId={conn.connectionId}]";
        NetworkServer.AddPlayerForConnection(conn, player);
        Debug.Log($"Player spawned for connection {conn.connectionId}");
    }

    private void BroadcastLobbyPlayers()
    {
        string[] players = connectedLobbyPlayers.Values.OrderBy(n => n).ToArray();

        if (NetworkServer.active)
        {
            NetworkServer.SendToAll(new LobbyPlayersSnapshotMessage { playerNames = players });
        }

        ApplyLobbyPlayersOnClient(players);
    }

    private void OnLobbyPlayersSnapshotMessage(LobbyPlayersSnapshotMessage msg)
    {
        ApplyLobbyPlayersOnClient(msg.playerNames ?? Array.Empty<string>());
    }

    private static void ApplyLobbyPlayersOnClient(IReadOnlyList<string> players)
    {
        latestLobbyPlayers.Clear();

        if (players != null)
        {
            for (int i = 0; i < players.Count; i++)
                latestLobbyPlayers.Add(players[i]);
        }

        LobbyPlayersUpdated?.Invoke(LatestLobbyPlayers);
    }

    private static string GetSceneName(string sceneReference)
    {
        if (string.IsNullOrWhiteSpace(sceneReference))
        {
            return string.Empty;
        }

        string normalized = sceneReference.Replace('\\', '/');
        if (normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || normalized.Contains('/'))
        {
            return Path.GetFileNameWithoutExtension(normalized);
        }

        return normalized;
    }
}
