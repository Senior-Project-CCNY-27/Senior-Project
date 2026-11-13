using DG.Tweening;
using UnityEngine;
using Sirenix.OdinInspector;

public class DoorOpener : MonoBehaviour, IInteractionValidator
{
    [Header("Door Settings")]
    [SerializeField, ReadOnly] private bool _isOpen = false;   // Determines whether the door starts open or closed.
    [SerializeField] private bool _isLocked = false; // Determines whether the door requires a key.
    [SerializeField] private float _animationDuration = 0.5f; // Controls how quickly the door rotates.
    [SerializeField] private Ease _animationEase = Ease.Linear; // Controls how quickly the door rotates.

    [SerializeField, Required] private Transform _doorRotator; // Transform responsible for rotating the door.

    [Header("Rotation Angles")]
    [SerializeField] private Vector3 _closedRotation = new Vector3(0, 0, 0); // Door rotation when closed.
    [SerializeField] private Vector3 _openRotation = new Vector3(0, -90, 0);   // Door rotation when open.
    
    private void Start()
    {
        AnimateDoor();
    }

    public bool CanInteract(Interactor interactor)
    {
        // If the door isn't locked, allow anyone to interact.
        if (!_isLocked)
            return true;

        if (interactor == null)
            return false;

        // Get the player's pickup controller.
        if (!interactor.TryGetComponent(out PlayerPickupController pickupController))
            return false;

        // Check if the player is holding something.
        Pickup heldItem = pickupController.HeldPickupItem;
        if (heldItem == null)
            return false;

        // Check if the held item is a key.
        return heldItem.TryGetComponent<Key>(out _);
    }

    public void ToggleDoor(Interactor interactor)
    {
        // Prevent interaction if the player doesn't have the required key.
        if (!CanInteract(interactor))
        {
            return;
        }

        // Toggle the door between open and closed.
        _isOpen = !_isOpen;
        
        AnimateDoor();
    }

    private void AnimateDoor()
    {
        _doorRotator.DOKill();
        
        Vector3 targetEulerAngles = _isOpen ? _openRotation : _closedRotation; 
        _doorRotator.DOLocalRotate(targetEulerAngles, _animationDuration).SetEase(_animationEase);
    }
}
