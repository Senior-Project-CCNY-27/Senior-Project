using NaughtyAttributes;
using UnityEngine;

public class MenuBacker : MonoBehaviour
{
    [SerializeField, Required] private Menu _menu;

    private void OnEnable()
    {
        InputManager.Instance.Actions.UI.Pause.performed += Input_Back;
    }

    private void OnDisable()
    {
        InputManager.Instance.Actions.UI.Pause.performed -= Input_Back;
    }

    private void Input_Back(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        _menu.BackOrClose();
    }
}