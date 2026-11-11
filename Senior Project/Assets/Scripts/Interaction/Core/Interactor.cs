using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class Interactor : MonoBehaviour
{
    [SerializeField] private float _castDistance = 5f; // how far the spherecast goes
    [SerializeField] private float _sphereCastRadius = 0.25f;

    [field: SerializeField, ReadOnly] public Interactable CurrentInteractable { get; private set; }    
    
    private Transform _mainCamera => Camera.main != null ? Camera.main.transform : null;
    private int _interactableLayerMask; // Only detect objects on the Interactable layer

    private void Awake()
    {
        _interactableLayerMask = LayerMask.GetMask("Interactable", "Wall");
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
        if (!CanInteractWithCurrentInteractable())
            return;
        
        CurrentInteractable.Interact(this);
    }
    
    private void Update()
    {
        CurrentInteractable = GetInteractable();
    }

    private void OnDrawGizmos()
    {
        Transform cameraTransform = _mainCamera;
        if (cameraTransform == null)
            return;

        Vector3 start = cameraTransform.position;
        Vector3 forward = cameraTransform.forward;
        Vector3 end = start + forward * _castDistance;
        Vector3 right = Vector3.Cross(forward, cameraTransform.up).normalized;
        Vector3 up = Vector3.Cross(right, forward).normalized;

        Gizmos.color = CurrentInteractable != null ? Color.green : Color.red;
        Gizmos.DrawWireSphere(start, _sphereCastRadius);
        Gizmos.DrawWireSphere(end, _sphereCastRadius);

        Gizmos.DrawLine(start + right * _sphereCastRadius, end + right * _sphereCastRadius);
        Gizmos.DrawLine(start - right * _sphereCastRadius, end - right * _sphereCastRadius);
        Gizmos.DrawLine(start + up * _sphereCastRadius, end + up * _sphereCastRadius);
        Gizmos.DrawLine(start - up * _sphereCastRadius, end - up * _sphereCastRadius);
    }

    /// <summary>
    /// Spherecasts to try and get the interactable that the interactor is looking at
    /// </summary>
    private Interactable GetInteractable()
    {
        if (Physics.SphereCast(_mainCamera.position, _sphereCastRadius, _mainCamera.forward, out RaycastHit hit,
                _castDistance, _interactableLayerMask))
            return hit.collider.GetComponent<Interactable>();

        return null;
    }

    public bool CanInteractWithCurrentInteractable()
    {
        // Cant interact without an interactable
        if (CurrentInteractable == null)
            return false;

        if (!CurrentInteractable.IsInteractionEnabled)
            return false;

        if (!CurrentInteractable.CanInteractAfterValidation(this))
            return false;

        return true;
    }
}
