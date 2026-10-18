using NaughtyAttributes;
using UnityEngine;

public class DeathScreenManager : Singleton<DeathScreenManager>
{
    [SerializeField, Required] private Menu _deathScreen;


    [Button("Open")]
    public void Open()
    {
        Menu.Focus(_deathScreen);
    }
}
