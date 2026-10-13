using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManagerActions : MonoBehaviour
{
    public void StartGameFromMainMenu()
    {
        GameManager.Instance.StartGameFromMainMenu();
    }

    public void ReturnToMainMenu()
    {
        GameManager.Instance.ReturnToMainMenu();
    }

    public void QuitGame()
    {
        GameManager.Quit();
    }

    public void ReloadScene()
    {
        SceneManager.LoadScene(GameManager.GetCurrentScene().name);
    }
}