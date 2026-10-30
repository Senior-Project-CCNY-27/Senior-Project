using UnityEngine;
using NaughtyAttributes;

public class DoorOpener : MonoBehaviour, IInteractionValidator
{
    [Header("Door Settings")]
    [SerializeField] private bool isOpen = false;   // Determines whether the door starts open or closed.
    [SerializeField] private bool isLocked = false; // Determines whether the door requires a key.
    [SerializeField] private float rotationSpeed = 5f; // Controls how quickly the door rotates.

    [SerializeField, Required] private Transform doorRatator; // Transform responsible for rotating the door.

    [Header("Rotation Angles")]
    [SerializeField] private Vector3 closedRotation = new Vector3(0, 90, 0); // Door rotation when closed.
    [SerializeField] private Vector3 openRotation = new Vector3(0, 0, 0);   // Door rotation when open.

    private Quaternion _targetRotation; // Stores the rotation the door should move towards.

    private void Start()
    {
        // Make sure a door transform has been assigned.
        if (doorRatator == null)
        {
            return;
        }

        // Set the initial rotation depending on whether the door starts open or closed.
        _targetRotation = Quaternion.Euler(isOpen ? openRotation : closedRotation);

        // Apply the initial rotation immediately.
        doorRatator.localRotation = _targetRotation;
    }

    private void Update()
    {
        // Make sure a door transform has been assigned.
        if (doorRatator == null)
        {
            return;
        }
            
        // Smoothly rotate the door towards its target rotation.
        doorRatator.localRotation = Quaternion.Slerp(doorRatator.localRotation, _targetRotation,rotationSpeed * Time.deltaTime);
    }

    public bool CanInteract(Interactor interactor)
    {
        // If the door isn't locked, allow anyone to interact.
        if (!isLocked)
        {
            return true;
        }

        // Get the KeyPickup component from the player.
        KeyPickup keyPickup = interactor.GetComponent<KeyPickup>();

        // Only allow interaction if the player has picked up the key.
        return keyPickup != null && keyPickup.keyPickedUp;
    }

    public void UseDoor(Interactor interactor)
    {
        // Prevent interaction if the player doesn't have the required key.
        if (!CanInteract(interactor))
        {
            return;
        }

        // Toggle the door between open and closed.
        isOpen = !isOpen;

        // Update the target rotation based on the door's new state.
        _targetRotation = Quaternion.Euler(isOpen ? openRotation : closedRotation);
    }
}
