using System;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Interactable))]
[RequireComponent(typeof(Rigidbody))]
public class Pickup : MonoBehaviour, IInteractionValidator
{
    private Interactable _interactable;
    public Rigidbody RigidBody { get; private set; }

    public bool IsPickedUp { get; private set; } = false;

    [field: Header("Events")]
    [field: SerializeField] public UnityEvent<PlayerPickupController> OnUse { get; private set; } = new();

    private void Awake()
    {
        _interactable = GetComponent<Interactable>();
        RigidBody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        _interactable.OnInteract.AddListener(ExecutePickup);
    }

    private void OnDisable()
    {
        _interactable.OnInteract.RemoveListener(ExecutePickup);
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
        PlayerPickupController pickupController = interactor.GetComponent<PlayerPickupController>();
        if (pickupController == null)
        {
            Debug.LogWarning($"Inventory is missing from interactor, can't pickup {name}");
            return;
        }

        pickupController.PickupItem(this);
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

    public void Use(PlayerPickupController pickupController)
    {
        OnUse?.Invoke(pickupController);
    }
}