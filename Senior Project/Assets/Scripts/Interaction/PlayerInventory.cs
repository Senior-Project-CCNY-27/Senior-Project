using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerInventory : MonoBehaviour
{
    [Header("Hand Settings")]
    [SerializeField, Required] private Transform _handTransform;
    [field: SerializeField, ReadOnly] public Pickup HeldPickupItem { get; private set; }
    public bool IsHandEmpty => HeldPickupItem == null;

    private void OnEnable()
    {
        InputManager.Instance.Actions.Player.Drop.performed += Input_Drop;
    }

    private void OnDisable()
    {
        InputManager.Instance.Actions.Player.Drop.performed -= Input_Drop;
    }
    
    private void Input_Drop(InputAction.CallbackContext context)
    {
        Drop();
    }

    public void PickupItem(Pickup pickup)
    {
        if (!IsHandEmpty)
            return;

        if (pickup == null)
            return;

        if (!pickup.TryAttach(_handTransform))
            return;
        
        HeldPickupItem = pickup;
    }

    public void Drop()
    {
        if (IsHandEmpty)
            return;

        if (!HeldPickupItem.TryDetach())
            return;
        
        HeldPickupItem = null;
    }
}
