using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// So that we can start in any scene and our game manager knows what state to use
/// </summary>
public class EditorGameStateStarter : MonoBehaviour
{
    [SerializeField] private GameManager.State _startingState;

#if UNITY_EDITOR
    private const string MainMenuSceneName = "MainMenu";
    private static Scene _editorStartingScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        _editorStartingScene = SceneManager.GetActiveScene();

        // Warn editors that their scene doesn't have a game state starter
        if (_editorStartingScene.name != MainMenuSceneName)
        {
            EditorGameStateStarter starter = FindFirstObjectByType<EditorGameStateStarter>();
            if (starter == null)
            {
                string message = $"Defaulting GameManager to None game state. " +
                                 $"If you want your scene '{_editorStartingScene.name}' to start in a different game state, please add a " +
                                 $"gameObject with EditorGameStateStarter attached to it";
                Debug.LogWarning(message);
                
                UnityEditor.EditorUtility.DisplayDialog("Missing Game State Starter", message, "Ok");
            }
        }
    }
#endif

    private void Start()
    {
#if UNITY_EDITOR
        if (gameObject.scene.path != _editorStartingScene.path)
            return;

        Debug.Log($"Editor scene {_editorStartingScene.name} started, automatically changing game state state to {_startingState}");
        GameManager.Instance.ChangeState(_startingState);
#endif
    }
}