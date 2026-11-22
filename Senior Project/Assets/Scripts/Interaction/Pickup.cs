using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Interactable))]
[RequireComponent(typeof(Rigidbody))]
public class Pickup : MonoBehaviour, IInteractionValidator
{
    private Interactable _interactable;
    private IPickupUseValidator[] _useValidators;
    public Rigidbody RigidBody { get; private set; }
    public Collider Collider { get; private set; }
    public PlayerPickupController PickupController { get; private set; }

    public bool IsPickedUp { get; private set; } = false;

    [field: Header("Use")]
    [field: SerializeField] public bool Usable { get; private set; } = false;
    [field: SerializeField, ShowIf("Usable")] public UnityEvent<PlayerPickupController> OnUse { get; private set; } = new();

    private void OnValidate()
    {
        if (RigidBody == null)
        {
            RigidBody = GetComponent<Rigidbody>();
            RigidBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
    }

    private void Awake()
    {
        _interactable = GetComponent<Interactable>();
        RigidBody = GetComponent<Rigidbody>();
        Collider = GetComponent<Collider>();
        _useValidators = GetComponentsInChildren<IPickupUseValidator>(true);
    }

    public bool CanInteract(Interactor interactor)
    {
        if (interactor == null)
            return false;

        if (!interactor.TryGetComponent(out PlayerPickupController inventory))
            return false;
        
        return inventory.IsHandEmpty;
    }
    
    public void ExecutePickup(Interactor interactor)
    {
        PickupController = interactor.GetComponent<PlayerPickupController>();
        if (PickupController == null)
        {
            Debug.LogWarning($"Inventory is missing from interactor, can't pickup {name}");
            return;
        }

        PickupController.PickupItem(this);
    }

    public bool TryAttach(Transform targetTransform)
    {
        if (RigidBody == null)
            return false;

        transform.SetParent(targetTransform);
        transform.position = targetTransform.position;
        transform.localRotation = Quaternion.identity;
        
        RigidBody.useGravity = false;
        RigidBody.constraints = RigidbodyConstraints.FreezeAll;
        
        _interactable.EnableInteractability(false);

        IsPickedUp = true;
        
        return true;
    }

    public bool TryDetach()
    {
        if (RigidBody == null)
            return false;
        
        transform.SetParent(null);
        RigidBody.useGravity = true;
        RigidBody.constraints = RigidbodyConstraints.None;
        
        _interactable.EnableInteractability(true);

        IsPickedUp = false;

        return true;
    }

    public bool TryUse(PlayerPickupController pickupController)
    {
        if (!Usable || !CanUseAfterValidation(pickupController))
            return false;

        OnUse?.Invoke(pickupController);
        return true;
    }

    public bool CanUseAfterValidation(PlayerPickupController pickupController)
    {
        if (pickupController == null)
            return false;

        foreach (IPickupUseValidator validator in _useValidators)
        {
            if (!validator.CanUse(pickupController))
                return false;
        }

        return true;
    }
}
