using System;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Interactable))]
[RequireComponent(typeof(Rigidbody))]
public class Pickup : MonoBehaviour, IInteractionValidator
{
    private Interactable _interactable;
    private Rigidbody _rigidbody;

    private void Awake()
    {
        _interactable = GetComponent<Interactable>();
        _rigidbody = GetComponent<Rigidbody>();
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

        if (!interactor.TryGetComponent(out PlayerInventory inventory))
            return false;
        
        return inventory.IsHandEmpty;
    }
    
    public void ExecutePickup(Interactor interactor)
    {
        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogWarning($"Inventory is missing from interactor, can't pickup {name}");
            return;
        }

        inventory.PickupItem(this);
    }

    public bool TryAttach(Transform targetTransform)
    {
        if (_rigidbody == null)
            return false;

        transform.SetParent(targetTransform);
        transform.position = targetTransform.position;
        transform.localRotation = Quaternion.identity;
        
        _rigidbody.useGravity = false;
        _rigidbody.constraints = RigidbodyConstraints.FreezeAll;
        
        _interactable.EnableInteractability(false);
        
        return true;
    }

    public bool TryDetach()
    {
        if (_rigidbody == null)
            return false;
        
        transform.SetParent(null);
        _rigidbody.useGravity = true;
        _rigidbody.constraints = RigidbodyConstraints.None;
        
        _interactable.EnableInteractability(true);
        
        return true;
    }
}