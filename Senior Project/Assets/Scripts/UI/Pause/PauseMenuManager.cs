using NaughtyAttributes;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Animancer.Easing;

public class PauseMenuManager : Singleton<PauseMenuManager>
{
    [SerializeField, Required] private Menu _pauseMenu;

    public void Open()
    {
        Menu.Focus(_pauseMenu);
    }
}
