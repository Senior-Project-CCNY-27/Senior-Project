using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class PlayerPickupController : MonoBehaviour
{
    [Header("Hand Settings")]
    [SerializeField, Required] private Transform _handTransform;
    [field: SerializeField, ReadOnly] public Pickup HeldPickupItem { get; private set; }
    public bool IsHandEmpty => HeldPickupItem == null;
    
    [Header("Throw Settings")]
    [SerializeField] private float _throwForce = 25f;

    private bool _isThrowing = false;

    private void OnEnable()
    {
        InputManager.Instance.Actions.Player.Interact.performed += Input_InteractPerformed;
        
        InputManager.Instance.Actions.Player.Drop.performed += Input_DropPerformed;
        InputManager.Instance.Actions.Player.Drop.canceled += Input_DropCanceled;
    }

    private void OnDisable()
    {
        InputManager.Instance.Actions.Player.Interact.performed -= Input_InteractPerformed;
        
        InputManager.Instance.Actions.Player.Drop.performed -= Input_DropPerformed;
        InputManager.Instance.Actions.Player.Drop.canceled -= Input_DropCanceled;
    }

    private void Input_InteractPerformed(InputAction.CallbackContext context)
    {
        UsePickup();
    }

    private void Input_DropPerformed(InputAction.CallbackContext context)
    {
        if (context.interaction is HoldInteraction)
        {
            _isThrowing = true;
            return;
        }
        
        if(context.interaction is PressInteraction)
            Drop();
    }
    
    private void Input_DropCanceled(InputAction.CallbackContext context)
    {
        if (_isThrowing && context.interaction is HoldInteraction)
            Throw();
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

    private void Drop()
    {
        if (IsHandEmpty)
            return;

        if (!HeldPickupItem.TryDetach())
            return;
        
        HeldPickupItem = null;
    }

    private void Throw()
    {
        if (IsHandEmpty)
            return;

        if (!HeldPickupItem.TryDetach())
            return;
        
        HeldPickupItem.RigidBody.AddForce(Camera.main.transform.forward * _throwForce, ForceMode.Impulse);
        HeldPickupItem = null;

        _isThrowing = false;
    }

    private void UsePickup()
    {
        if (IsHandEmpty)
            return;

        if (!HeldPickupItem.TryUse(this))
            return;
    }

    public void ConsumePickup()
    {
        if (IsHandEmpty)
            return;
        
        Destroy(HeldPickupItem.gameObject);
        HeldPickupItem = null;
    }
}
