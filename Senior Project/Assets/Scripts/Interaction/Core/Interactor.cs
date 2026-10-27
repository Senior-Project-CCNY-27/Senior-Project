using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class Interactor : MonoBehaviour
{
    [SerializeField] private float _castDistance = 5f; // how far the raycast goes

    [field: SerializeField, ReadOnly] public Interactable CurrentInteractable { get; private set; }    
    
    private Transform _mainCamera => Camera.main.transform;
    private int _interactableLayerMask; // Only detect objects on the Interactable layer

    private void Awake()
    {
        _interactableLayerMask = LayerMask.GetMask("Interactable");
    }

    private void OnEnable()
    {
        InputManager.Instance.Actions.Player.Interact.performed += Input_OnInteract;
    }

    private void OnDisable()
    {
        if(InputManager.Instance != null)
            InputManager.Instance.Actions.Player.Interact.performed -= Input_OnInteract;
    }

    /// <summary>
    /// Called whenever the Interact key is pressed
    /// </summary>
    private void Input_OnInteract(InputAction.CallbackContext context)
    {
        // Cant interact without an interactable
        if (CurrentInteractable == null)
            return;

        if (!CurrentInteractable.IsInteractionEnabled)
            return;

        if (!CurrentInteractable.CanInteractAfterValidation(this))
            return;
        
        CurrentInteractable.Interact(this);
    }
    
    private void Update()
    {
        CurrentInteractable = GetInteractable();
    }

    private void OnDrawGizmos()
    {
        // Shows the raycast in the Scene view
        Gizmos.DrawLine(_mainCamera.position, _mainCamera.position + _mainCamera.forward * _castDistance);
    }

    /// <summary>
    /// Raycasts to try and get the interactable that the interactor is looking at
    /// </summary>
    private Interactable GetInteractable()
    {
        if (Physics.Raycast(_mainCamera.position, _mainCamera.forward, out RaycastHit hit, _castDistance,
                _interactableLayerMask))
            return hit.collider.GetComponent<Interactable>();

        return null;
    }
}
