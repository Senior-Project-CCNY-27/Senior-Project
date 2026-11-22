using System;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class PlayerPickupController : MonoBehaviour
{
    [Header("Hand Settings")]
    [SerializeField, Required] private Transform _holdOrigin;
    [SerializeField, Required] private AnimancerComponent _animator;
    [SerializeField, Required] private TwoBoneIKConstraint _holdIK;
    [SerializeField, Required] private Transform _holdIKTarget;
    private Vector3 _holdOriginBaseLocalPosition;
    [field: SerializeField, ReadOnly] public Pickup HeldPickupItem { get; private set; }
    public bool IsHandEmpty => HeldPickupItem == null;
    private PlayableOutputRefresher _playableOutputRefresher;
    
    [Header("Throw Settings")]
    [SerializeField] private FloatRange _throwForceRange = new FloatRange(2f, 20f);
    [field: SerializeField] public float ThrowHoldMaxDuration { get; private set; } = 1f;

    public bool IsThrowing { get; private set; }
    public float ThrowHoldTimer { get; private set; }

    private void Awake()
    {
        _holdOriginBaseLocalPosition = _holdOrigin.localPosition;
    }

    private void Start()
    {
        _playableOutputRefresher = new(_animator);
        SetPickupIKWeight(0f);
        
        // Make the hold IK target parented by the cam
        Transform camTransform = Camera.main.transform;
        Vector3 holdLocalPosition = transform.InverseTransformPoint(_holdIKTarget.position);
        holdLocalPosition.y = _holdIKTarget.position.y - camTransform.position.y;
        Quaternion holdLocalRotation = Quaternion.Inverse(transform.rotation) * _holdIKTarget.rotation;
        _holdIKTarget.SetParent(camTransform, false);
        _holdIKTarget.localPosition = holdLocalPosition;
        _holdIKTarget.localRotation = holdLocalRotation;
    }

    private void OnEnable()
    {
        InputManager.Instance.Actions.Player.Use.performed += Input_UsePerformed;
        
        InputManager.Instance.Actions.Player.Drop.performed += Input_DropPerformed;
        InputManager.Instance.Actions.Player.Drop.canceled += Input_DropCanceled;
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.Actions.Player.Use.performed -= Input_UsePerformed;
            
            InputManager.Instance.Actions.Player.Drop.performed -= Input_DropPerformed;
            InputManager.Instance.Actions.Player.Drop.canceled -= Input_DropCanceled;
        }
    }

    private void Update()
    {
        UpdateThrowTimer();
    }

    private void Input_UsePerformed(InputAction.CallbackContext context)
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

        AdjustHoldOriginForPickup(pickup);
        if (!pickup.TryAttach(_holdOrigin))
        {
            ResetHoldOriginPosition();
            return;
        }
        
        HeldPickupItem = pickup;
        
        SetPickupIKWeight(1f);
    }

    private void Drop()
    {
        if (IsHandEmpty)
            return;

        if (!HeldPickupItem.TryDetach())
            return;
        
        ClearHeldPickupItem();
    }

    private void Throw()
    {
        if (IsHandEmpty)
            return;

        if (!HeldPickupItem.TryDetach())
            return;

        float throwForce = _throwForceRange.Lerp(ThrowHoldTimer / ThrowHoldMaxDuration);
        HeldPickupItem.RigidBody.AddForce(Camera.main.transform.forward * throwForce, ForceMode.Impulse);
        ClearHeldPickupItem();

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

    private bool UsePickup()
    {
        Pickup pickup = HeldPickupItem;
        if (pickup == null)
            return false;

        return pickup.TryUse(this);
    }

    public void ConsumePickup()
    {
        if (IsHandEmpty)
            return;
        
        Destroy(HeldPickupItem.gameObject);
        ClearHeldPickupItem();
    }

    private void ClearHeldPickupItem()
    {
        HeldPickupItem = null;
        ResetHoldOriginPosition();
        SetPickupIKWeight(0f);
    }

    private void AdjustHoldOriginForPickup(Pickup pickup)
    {
        if (pickup.Collider == null)
        {
            ResetHoldOriginPosition();
            return;
        }

        Bounds colliderBounds = pickup.Collider.bounds;
        Vector3 boundsCenterInPickupSpace = pickup.transform.InverseTransformPoint(colliderBounds.center);
        Vector3 centerOffset = new(boundsCenterInPickupSpace.x, boundsCenterInPickupSpace.y, 0f);
        float forwardOffset = colliderBounds.extents.z;

        _holdOrigin.localPosition = _holdOriginBaseLocalPosition
            - centerOffset
            + Vector3.forward * forwardOffset;
    }

    private void ResetHoldOriginPosition()
    {
        _holdOrigin.localPosition = _holdOriginBaseLocalPosition;
    }

    private void SetPickupIKWeight(float weight)
    {
        _holdIK.weight = weight;
        _playableOutputRefresher.Refresh();
    }
}
