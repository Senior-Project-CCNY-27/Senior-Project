using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject pauseMenu;

    [Header("PauseSetting")]
    [SerializeField] private string mainMenuScene;
    [SerializeField] private string gameplayScene;
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

    public bool isPaused;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pauseMenu.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        //Pause and Resume game when assigned Key is pressed
        if (Input.GetKeyDown(pauseKey))
        {
            if (isPaused) Resume();
            else Pause();
        }
    }
    
    /// <summary>
    /// Sets the timeScale to 0 and unlocks the cursor
    /// </summary>
    public void Pause()
    {
        pauseMenu.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Sets the timeScale to 1 and locks the cursor
    /// </summary>
    public void Resume()
    {
        pauseMenu.SetActive(false);
        Time.timeScale = 1.0f;
        isPaused = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// Loads the Gameplay Scene 
    /// </summary>
    public void Restart()
    {
        Time.timeScale = 1.0f;
        isPaused = false;
        SceneManager.LoadScene(gameplayScene);
    }

    /// <summary>
    /// Loads the MainMenu Scene
    /// </summary>
    public void LoadMainMenu()
    {
        Time.timeScale = 1.0f;
        isPaused = false;
        SceneManager.LoadScene(mainMenuScene);
    }
}
