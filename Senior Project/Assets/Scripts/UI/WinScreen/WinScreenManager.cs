using NaughtyAttributes;
using UnityEngine;

public class WinScreenManager : Singleton<WinScreenManager>
{
    [SerializeField, Required] private Menu _winScreen;


    [Button("Open")]
    public void Open()
    {
        Menu.Focus(_winScreen);
    }
}
