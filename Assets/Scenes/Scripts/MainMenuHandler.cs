using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuHandler : MonoBehaviour
{
    [Header("Scene Targets")]
    [Scene] [SerializeField] private string singlePlayerScene;
    [Scene] [SerializeField] private string multiplayerScene;

    public void onSinglePlayerClicked()
    {
        Debug.Log("Single Player button clicked");

        if (string.IsNullOrWhiteSpace(singlePlayerScene))
        {
            Debug.LogError("Single player scene is not set on MainMenuHandler.");
            return;
        }

        SceneManager.LoadScene(singlePlayerScene);
    }

    public void onMultiplayerClicked()
    {
        Debug.Log("Multiplayer button clicked");

        if (string.IsNullOrWhiteSpace(multiplayerScene))
        {
            Debug.LogError("Multiplayer scene is not set on MainMenuHandler.");
            return;
        }

        SceneManager.LoadScene(multiplayerScene);
    }

    public void onQuitClicked()
    {
        Debug.Log("Quit button clicked");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

}
