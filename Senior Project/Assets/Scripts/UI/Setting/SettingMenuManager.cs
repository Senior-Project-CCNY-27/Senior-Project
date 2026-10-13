using NaughtyAttributes;
using UnityEngine;

public class SettingMenuManager : Singleton<SettingMenuManager>
{
    [SerializeField, Required] private Menu _settingMenu;

    public void Open()
    {
        Menu.Focus(_settingMenu);
    }
}
