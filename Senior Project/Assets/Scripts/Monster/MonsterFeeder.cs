using Sirenix.OdinInspector;
using UnityEngine;

public class MonsterFeeder : MonoBehaviour, IInteractionValidator
{
    [SerializeField, Required] private StateMachine _stateMachine;
    [SerializeField, Required] private MonsterStateActions _stateActions;
    [SerializeField, Required] private State _allowedRootState;
    
    private PlayerPickupController _pickupController;
    
    public bool CanInteract(Interactor interactor)
    {
        if (_pickupController == null)
            _pickupController = interactor.GetComponent<PlayerPickupController>();

        // The allowed root state is chosen in the Inspector.
        if (_stateMachine.CurrentState != _allowedRootState)
            return false;
        
        if (_pickupController.IsHandEmpty)
            return false;
        
        return _pickupController.HeldPickupItem.CompareTag("Food");
    }

    public void Feed(Interactor interactor)
    {
        if (_pickupController == null)
            _pickupController = interactor.GetComponent<PlayerPickupController>();
        
        if (_pickupController.IsHandEmpty)
            return;
        
        _stateActions.Feed(_pickupController.HeldPickupItem.gameObject);
        _pickupController.ConsumePickup();
    }
}
