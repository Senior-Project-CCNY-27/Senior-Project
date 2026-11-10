using DavidFDev.DevConsole;
using UnityEngine;

/// <summary>
/// Temporarily releases the cursor and disables game input while the developer console is open.
/// Attach this alongside InputManager on the InputManager prefab.
/// </summary>
public class DevConsoleInputBridge : MonoBehaviour
{
    private bool _consoleOpen;

    private void OnEnable()
    {
        DevConsole.OnConsoleOpened += HandleConsoleOpened;
        DevConsole.OnConsoleClosed += HandleConsoleClosed;
    }

    private void OnDisable()
    {
        DevConsole.OnConsoleOpened -= HandleConsoleOpened;
        DevConsole.OnConsoleClosed -= HandleConsoleClosed;

        if (_consoleOpen)
            RestoreCursor();
    }

    private void HandleConsoleOpened()
    {
        if (_consoleOpen)
            return;

        _consoleOpen = true;
        InputManager.Instance?.SetCursorLockOverride(CursorLockMode.None);
        InputManager.Instance?.SetActionsOverride(false);
    }

    private void HandleConsoleClosed()
    {
        if (!_consoleOpen)
            return;

        RestoreCursor();
    }

    private void RestoreCursor()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.SetCursorLockOverride(null);
            InputManager.Instance.SetActionsOverride(null);
        }

        _consoleOpen = false;
    }
}
