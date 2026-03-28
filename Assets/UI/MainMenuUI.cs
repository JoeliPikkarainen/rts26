using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public void OnSinglePlayerClicked()
    {
        Debug.Log("Loading Single Player...");
        SceneManager.LoadScene("GeneratorScene");
    }

    public void OnMultiplayerClicked()
    {
        Debug.Log("Loading Multiplayer...");
        SceneManager.LoadScene("ServerSceneSimple");
    }

    public void OnSettingsClicked()
    {
        Debug.Log("Settings (not implemented yet)");
        // TODO: Open settings menu
    }

    public void OnQuitClicked()
    {
        Debug.Log("Quitting game...");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
