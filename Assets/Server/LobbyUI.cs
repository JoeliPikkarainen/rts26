using System.Text;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this to any GameObject in MultiPlayerLobbyScene alongside your TMP text field.
/// It subscribes to GameNetworkManager's lobby snapshot event so every client sees the same player list.
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private Button startMatchButton;

    void OnEnable()
    {
        GameNetworkManager.LobbyPlayersUpdated += OnLobbyPlayersUpdated;
        RefreshText(GameNetworkManager.LatestLobbyPlayers);

        if (startMatchButton != null)
            startMatchButton.interactable = NetworkServer.active;
    }

    void OnDisable()
    {
        GameNetworkManager.LobbyPlayersUpdated -= OnLobbyPlayersUpdated;
    }

    private void OnLobbyPlayersUpdated(IReadOnlyList<string> players)
    {
        RefreshText(players);
    }

    public void OnStartMatchClicked()
    {
        if (!(NetworkManager.singleton is GameNetworkManager manager))
        {
            Debug.LogError("NetworkManager.singleton is not a GameNetworkManager.");
            return;
        }

        manager.StartGameFromLobby();
    }

    private void RefreshText(IReadOnlyList<string> names)
    {
        if (playerListText == null)
            return;

        if (names == null || names.Count == 0)
        {
            playerListText.text = "No players connected.";
            return;
        }

        StringBuilder sb = new StringBuilder();
        foreach (string name in names)
            sb.AppendLine(name);

        playerListText.text = sb.ToString().TrimEnd();
    }
}
