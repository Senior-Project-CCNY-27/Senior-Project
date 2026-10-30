using DG.Tweening;
using UnityEngine;
using NaughtyAttributes;

public class DoorOpener : MonoBehaviour, IInteractionValidator
{
    [Header("Door Settings")]
    [SerializeField, ReadOnly] private bool isOpen = false;   // Determines whether the door starts open or closed.
    [SerializeField] private bool isLocked = false; // Determines whether the door requires a key.
    [SerializeField] private float animationDuration = 0.5f; // Controls how quickly the door rotates.
    [SerializeField] private Ease animationEase = Ease.Linear; // Controls how quickly the door rotates.

    [SerializeField, Required] private Transform doorRotator; // Transform responsible for rotating the door.

    [Header("Rotation Angles")]
    [SerializeField] private Vector3 closedRotation = new Vector3(0, 90, 0); // Door rotation when closed.
    [SerializeField] private Vector3 openRotation = new Vector3(0, 0, 0);   // Door rotation when open.

    private void Start()
    {
        AnimateDoor();
    }

    public bool CanInteract(Interactor interactor)
    {
        // If the door isn't locked, allow anyone to interact.
        if (!isLocked)
        {
            return true;
        }

        // Get the KeyPickup component from the player.
        KeyHolder keyHolder = interactor.GetComponent<KeyHolder>();
        if (keyHolder == null)
            return false;

        // Only allow interaction if the player has picked up the key.
        return keyHolder.KeyPickedUp;
    }

    public void ToggleDoor(Interactor interactor)
    {
        // Prevent interaction if the player doesn't have the required key.
        if (!CanInteract(interactor))
        {
            return;
        }

        // Toggle the door between open and closed.
        isOpen = !isOpen;
        
        AnimateDoor();
    }

    private void AnimateDoor()
    {
        doorRotator.DOKill();
        
        Vector3 targetEulerAngles = isOpen ? openRotation : closedRotation; 
        doorRotator.DOLocalRotate(targetEulerAngles, animationDuration).SetEase(animationEase);
    }
}
