using UnityEngine;

public class SettingOpener : MonoBehaviour
{
    public void OpenSetting()
    {
        SettingMenuManager.Instance.Open();
    }
}