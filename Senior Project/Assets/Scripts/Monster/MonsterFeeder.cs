using NaughtyAttributes;
using UnityEngine;

public class MonsterFeeder : MonoBehaviour, IInteractionValidator
{
    [SerializeField, Required] private MonsterController _monsterController;
    
    private PlayerPickupController _pickupController;
    
    public bool CanInteract(Interactor interactor)
    {
        if (_pickupController == null)
            _pickupController = interactor.GetComponent<PlayerPickupController>();

        // Can't feed when not calm
        if (_monsterController.StateMachine.CurrentState != _monsterController.CalmSuperState)
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
        
        _monsterController.Feed(_pickupController.HeldPickupItem.gameObject);
        _pickupController.ConsumePickup();
    }
}