using UnityEngine;
using NaughtyAttributes;

public class DoorOpener : MonoBehaviour, IInteractionValidator
{
   [Header("Door Settings")]
    [SerializeField] private bool isOpen = false;
    [SerializeField] private bool isLocked = false;
    [SerializeField] private float rotationSpeed = 5f;

    [SerializeField, Required] private Transform doorRatator;

    [Header("Rotation Angles")]
    [SerializeField] private Vector3 closedRotation = new Vector3(0, 90, 0);
    [SerializeField] private Vector3 openRotation = new Vector3(0, 0, 0);

    private Quaternion _targetRotation;

    private void Start()
    {
        if (doorRatator == null)
            return;

        _targetRotation = Quaternion.Euler(isOpen ? closedRotation : openRotation);

        doorRatator.localRotation = _targetRotation;
    }

    private void Update()
    {
        if (doorRatator == null)
            return;

        doorRatator.localRotation = Quaternion.Slerp(doorRatator.localRotation, _targetRotation, rotationSpeed * Time.deltaTime);
    }

    public bool CanInteract(Interactor interactor)
    {
        // Anyone can interact with the door.
        return true;
    }

    public void UseDoor(Interactor interactor)
    {
        isOpen = !isOpen;

        _targetRotation = Quaternion.Euler(isOpen ? closedRotation : openRotation);
    }
}
