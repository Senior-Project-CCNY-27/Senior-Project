using NaughtyAttributes;
using UnityEngine;

public class DeathScreenManager : Singleton<DeathScreenManager>
{
    [SerializeField, Required] private Menu _deathScreen;

    public void Open()
    {
        Menu.Focus(_deathScreen);
    }
}
