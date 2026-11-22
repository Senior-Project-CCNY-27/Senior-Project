using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class PlayerPickupController : MonoBehaviour
{
    [Header("Hand Settings")]
    [SerializeField, Required] private Transform _holdTransform;
    [field: SerializeField, ReadOnly] public Pickup HeldPickupItem { get; private set; }
    public bool IsHandEmpty => HeldPickupItem == null;
    
    [Header("Throw Settings")]
    [SerializeField] private FloatRange _throwForceRange = new FloatRange(2f, 20f);
    [field: SerializeField] public float ThrowHoldMaxDuration { get; private set; } = 1f;

    public bool IsThrowing { get; private set; }
    public float ThrowHoldTimer { get; private set; }

    private void Start()
    {
        Transform camTransform = Camera.main.transform;
        Vector3 holdLocalPosition = transform.InverseTransformPoint(_holdTransform.position);
        holdLocalPosition.y = _holdTransform.position.y - camTransform.position.y;
        Quaternion holdLocalRotation = Quaternion.Inverse(transform.rotation) * _holdTransform.rotation;

        _holdTransform.SetParent(camTransform, false);
        _holdTransform.localPosition = holdLocalPosition;
        _holdTransform.localRotation = holdLocalRotation;
    }

    private void OnEnable()
    {
        InputManager.Instance.Actions.Player.Interact.performed += Input_InteractPerformed;
        
        InputManager.Instance.Actions.Player.Drop.performed += Input_DropPerformed;
        InputManager.Instance.Actions.Player.Drop.canceled += Input_DropCanceled;
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.Actions.Player.Interact.performed -= Input_InteractPerformed;
            
            InputManager.Instance.Actions.Player.Drop.performed -= Input_DropPerformed;
            InputManager.Instance.Actions.Player.Drop.canceled -= Input_DropCanceled;
        }
    }

    private void Update()
    {
        UpdateThrowTimer();
    }

    private void Input_InteractPerformed(InputAction.CallbackContext context)
    {
        UsePickup();
    }

    private void Input_DropPerformed(InputAction.CallbackContext context)
    {
        if (context.interaction is HoldInteraction)
        {
            IsThrowing = true;
            return;
        }
        
        if(context.interaction is PressInteraction)
            Drop();
    }
    
    private void Input_DropCanceled(InputAction.CallbackContext context)
    {
        if (IsThrowing && context.interaction is HoldInteraction)
            Throw();
    }

    public void PickupItem(Pickup pickup)
    {
        if (!IsHandEmpty)
            return;

        if (pickup == null)
            return;

        if (!pickup.TryAttach(_holdTransform))
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

        float throwForce = _throwForceRange.Lerp(ThrowHoldTimer / ThrowHoldMaxDuration);
        HeldPickupItem.RigidBody.AddForce(Camera.main.transform.forward * throwForce, ForceMode.Impulse);
        HeldPickupItem = null;

        IsThrowing = false;
        ThrowHoldTimer = 0f;
    }

    private void UpdateThrowTimer()
    {
        if (!IsThrowing)
        {
            ThrowHoldTimer = 0f;
            return;
        }
        
        if(ThrowHoldTimer < ThrowHoldMaxDuration)
            ThrowHoldTimer += Time.deltaTime;
        else
            ThrowHoldTimer = ThrowHoldMaxDuration;
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
