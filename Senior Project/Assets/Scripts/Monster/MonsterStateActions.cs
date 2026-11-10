using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(StateMachine))]
public class MonsterStateActions : MonoBehaviour
{
    [SerializeField, Required] private StateMachine _stateMachine;
    [SerializeField, Required] private TransitionState _onForceAgitatedState;
    [SerializeField, Required] private TransitionState _onForceAggressiveState;
    [SerializeField, Required] private TransitionState _onFeedState;

    [Button("Force Agitated", ButtonSizes.Large)]
    public void ForceAgitated() => _stateMachine.ChangeState(_onForceAgitatedState);

    [Button("Force Aggressive", ButtonSizes.Large)]
    public void ForceAggressive() => _stateMachine.ChangeState(_onForceAggressiveState);

    [Button("Feed", ButtonSizes.Large)]
    public void Feed(GameObject foodObject = null)
    {
        if (foodObject != null && foodObject.TryGetComponent(out Interactable interactable))
            interactable.EnableInteractability(false);
        _onFeedState.SetTarget(foodObject);
        _stateMachine.ChangeState(_onFeedState);
    }
}
