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
    private enum GameplayWorldMode
    {
        WorldCreator,
        CustomTerrainPrefab
    }

    [Header("Scenes")]
    [Scene] [SerializeField] private string multiplayerLobbyScene = "MultiPlayerLobbyScene";
    [Scene] [SerializeField] private string multiplayerGameScene = "GeneratorScene";

    [Header("Gameplay World")]
    [SerializeField] private GameplayWorldMode gameplayWorldMode = GameplayWorldMode.WorldCreator;
    [SerializeField] private GameObject customTerrainPrefab;
    [SerializeField] private Vector3 customTerrainPosition = Vector3.zero;
    [SerializeField] private Vector3 customTerrainRotationEuler = Vector3.zero;
    [SerializeField] private float customTerrainSpawnHeightOffset = 2f;

    [Header("Lobby")]
    [SerializeField] private bool autoStartWhenHostConnects;

    private bool matchStarted;

    [Header("Room")]
    [SerializeField] private NetworkRoomPlayer roomPlayerPrefab = null;

    [UnitHeaderInspectable("Game")]
    [SerializeField] private NetworkPlayer gamePlayerPrefab = null;

    private readonly Dictionary<int, string> connectedLobbyPlayers = new Dictionary<int, string>();
    private bool pendingGameSceneStart;
    private bool pendingGameplayWorldInitialization;
    private GameObject spawnedCustomTerrain;
    private int spawnedPlayerCount;

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

        // During scene transition, wait until all current clients are ready before
        // generating world objects and starting the match.
        TryInitializeGameplayWorldAndMatch();
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

        // Host is both server and client. Server already generated the full world,
        // so skip client-only terrain generation to avoid clearing it.
        if (NetworkServer.active)
        {
            return;
        }

        if (!string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, GetSceneName(multiplayerGameScene), StringComparison.Ordinal))
        {
            return;
        }

        if (gameplayWorldMode == GameplayWorldMode.CustomTerrainPrefab)
        {
            EnsureCustomTerrainForClient();
            return;
        }

        WorldCreator worldCreator = FindObjectOfType<WorldCreator>();
        if (worldCreator != null)
        {
            worldCreator.RegisterNetworkSpawnPrefabsOnClient();

            // Client only generates terrain (ground mesh); interactive objects arrive via Mirror from server
            worldCreator.GenerateTerrain();
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
        pendingGameplayWorldInitialization = true;
        TryInitializeGameplayWorldAndMatch();
    }

    private void TryInitializeGameplayWorldAndMatch()
    {
        if (!pendingGameplayWorldInitialization)
        {
            return;
        }

        if (!AreAllConnectionsReady())
        {
            return;
        }

        pendingGameplayWorldInitialization = false;
        PrepareGameplayWorld();
        StartMatch();
    }

    private bool AreAllConnectionsReady()
    {
        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
        {
            if (connection == null)
            {
                continue;
            }

            if (!connection.isReady)
            {
                return false;
            }
        }

        return true;
    }

    private void PrepareGameplayWorld()
    {
        if (gameplayWorldMode == GameplayWorldMode.CustomTerrainPrefab)
        {
            SpawnCustomTerrainOnServer();
            return;
        }

        WorldCreator worldCreator = FindObjectOfType<WorldCreator>();
        if (worldCreator == null)
        {
            Debug.LogWarning("No WorldCreator found in gameplay scene. Players will spawn using existing start positions.");
            return;
        }

        worldCreator.GenerateWorld();
    }

    private void SpawnCustomTerrainOnServer()
    {
        if (customTerrainPrefab == null)
        {
            Debug.LogWarning("Custom terrain mode is active but no customTerrainPrefab is assigned.");
            return;
        }

        if (spawnedCustomTerrain != null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(customTerrainRotationEuler);
        spawnedCustomTerrain = Instantiate(customTerrainPrefab, customTerrainPosition, rotation);
        spawnedCustomTerrain.name = customTerrainPrefab.name;

        if (spawnedCustomTerrain.TryGetComponent(out NetworkIdentity identity))
        {
            if (!identity.isServer)
            {
                NetworkServer.Spawn(spawnedCustomTerrain);
            }
        }
        else
        {
            Debug.LogWarning("Custom terrain prefab has no NetworkIdentity. It will only exist on server/host unless clients also instantiate it locally.");
        }
    }

    private void EnsureCustomTerrainForClient()
    {
        if (customTerrainPrefab == null)
        {
            return;
        }

        if (spawnedCustomTerrain != null)
        {
            return;
        }

        if (customTerrainPrefab.TryGetComponent(out NetworkIdentity _))
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(customTerrainRotationEuler);
        spawnedCustomTerrain = Instantiate(customTerrainPrefab, customTerrainPosition, rotation);
        spawnedCustomTerrain.name = customTerrainPrefab.name;
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
        GameObject player;
        if (spawnPoint != null)
        {
            player = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
        }
        else if (TryGetFallbackSpawnPose(conn.connectionId, out Vector3 fallbackPosition, out Quaternion fallbackRotation))
        {
            player = Instantiate(prefabToSpawn, fallbackPosition, fallbackRotation);
        }
        else
        {
            player = Instantiate(prefabToSpawn);
        }

        spawnedPlayerCount++;

        player.name = $"{prefabToSpawn.name} [connId={conn.connectionId}]";
        NetworkServer.AddPlayerForConnection(conn, player);
        Debug.Log($"Player spawned for connection {conn.connectionId}");
    }

    private bool TryGetFallbackSpawnPose(int connectionId, out Vector3 position, out Quaternion rotation)
    {
        rotation = Quaternion.identity;

        if (gameplayWorldMode == GameplayWorldMode.CustomTerrainPrefab && TryGetCustomTerrainSpawnPosition(connectionId, out position))
        {
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    private bool TryGetCustomTerrainSpawnPosition(int connectionId, out Vector3 position)
    {
        position = Vector3.zero;

        GameObject terrainRoot = spawnedCustomTerrain;
        if (terrainRoot == null)
        {
            return false;
        }

        if (!TryGetWorldBounds(terrainRoot, out Bounds bounds))
        {
            return false;
        }

        int seed = (connectionId * 73856093) ^ (spawnedPlayerCount * 19349663);
        System.Random rng = new System.Random(seed);

        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        float minZ = bounds.min.z;
        float maxZ = bounds.max.z;

        float padX = Mathf.Max(1f, (maxX - minX) * 0.08f);
        float padZ = Mathf.Max(1f, (maxZ - minZ) * 0.08f);

        minX += padX;
        maxX -= padX;
        minZ += padZ;
        maxZ -= padZ;

        if (minX > maxX)
        {
            float centerX = bounds.center.x;
            minX = centerX;
            maxX = centerX;
        }

        if (minZ > maxZ)
        {
            float centerZ = bounds.center.z;
            minZ = centerZ;
            maxZ = centerZ;
        }

        float x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
        float z = Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble());
        Vector3 rayStart = new Vector3(x, bounds.max.y + 200f, z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
        {
            position = hit.point + Vector3.up * Mathf.Max(0.5f, customTerrainSpawnHeightOffset);
            return true;
        }

        position = new Vector3(x, bounds.max.y + Mathf.Max(0.5f, customTerrainSpawnHeightOffset), z);
        return true;
    }

    private static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (!col.enabled)
            {
                continue;
            }

            if (bounds.size == Vector3.zero)
            {
                bounds = col.bounds;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        if (bounds.size != Vector3.zero)
        {
            return true;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!renderer.enabled)
            {
                continue;
            }

            if (bounds.size == Vector3.zero)
            {
                bounds = renderer.bounds;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds.size != Vector3.zero;
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
