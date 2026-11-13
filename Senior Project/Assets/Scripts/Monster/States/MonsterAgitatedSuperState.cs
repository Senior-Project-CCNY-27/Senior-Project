using Sirenix.OdinInspector;
using UnityEngine;

namespace MonsterStates
{
    // Aggression behavior can own any collection of child states.
    public class MonsterAgitatedSuperState : SuperState
    {
        [Header("References")]
        [SerializeField, Required] private MonsterController _monster;

        [Header("Transitions")]
        [SerializeField] private State _onAggressionState;
        [SerializeField] private State _catchupState;
        [SerializeField, Required] private TransitionState _onPlayerTouchState;

        [Header("Catchup")]
        [SerializeField] private float _catchupDistance = 17.5f;

        [field: Header("Aggression")]
        [field: SerializeField] public float AggressiveThreshold { get; private set; } = 100f;
        [field: SerializeField, ReadOnly] public float CurrentAggression { get; private set; }
        [SerializeField] private float _hungryAggressionSpeed = 5f;
        [SerializeField] private float _playerHoldingFoodAggressionSpeed = 25f;
        [SerializeField] private float _touchAggressionBuildup = 25f;

        private protected override void OnEnter()
        {
            base.OnEnter();

            CurrentAggression = 0;
        }

        public override void HandleCollision(Collision collision)
        {
            if(collision.gameObject == _monster.PlayerPickupController.gameObject)
                TryHandlePlayerTouch();

            base.HandleCollision(collision);
        }

        private void TryHandlePlayerTouch()
        {
            if (!IsRunning || _onPlayerTouchState == null || SubStateMachine.HasTransitionedThisFrame)
                return;

            if (SubStateMachine.CurrentState == _onPlayerTouchState)
                return;

            _onPlayerTouchState.SetTarget(_monster.PlayerPickupController.gameObject);
            SubStateMachine.ChangeState(_onPlayerTouchState);
            if (SubStateMachine.CurrentState != _onPlayerTouchState)
                return;

            CurrentAggression += _touchAggressionBuildup;
        }

        private protected override void OnTick()
        {
            // Keep the child choice editable: assign a catchup state or leave it empty.
            if (_catchupState != null && SubStateMachine.CurrentState != _onPlayerTouchState &&
                Vector3.Distance(_monster.transform.position, _monster.PlayerTransform.position) > _catchupDistance)
                SubStateMachine.ChangeState(_catchupState);

            CurrentAggression += _hungryAggressionSpeed * Time.deltaTime;
            if (!_monster.PlayerPickupController.IsHandEmpty &&
                _monster.PlayerPickupController.HeldPickupItem.CompareTag("Food"))
                CurrentAggression += _playerHoldingFoodAggressionSpeed * Time.deltaTime;

            base.OnTick();
        }

        private void OnDrawGizmos()
        {
            if (!ShouldDrawGizmos)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_monster.transform.position, _catchupDistance);
        }

        private protected override State GetTransition() =>
            SubStateMachine.CurrentState != _onPlayerTouchState && CurrentAggression > AggressiveThreshold
                ? _onAggressionState
                : null;
    }
}
