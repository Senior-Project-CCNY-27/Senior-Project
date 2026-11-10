using Sirenix.OdinInspector;
using UnityEngine;

namespace MonsterStates
{
    // Agitation behavior can own any collection of child states.
    public class MonsterCalmSuperState : SuperState
    {
        [Header("References")]
        [SerializeField, Required] private MonsterController _monster;

        [Header("Transitions")]
        [SerializeField] private State _onAgitationState;
        [SerializeField] private State _catchupState;

        [field: Header("Catchup")]
        [field: SerializeField] public float CatchupDistance { get; private set; } = 10f;

        [field: Header("Agitation")]
        [field: SerializeField] public float AgitateThreshold { get; private set; } = 50f;
        [field: SerializeField, ReadOnly] public float CurrentAgitation { get; private set; }
        [SerializeField] private float _hungryAgitationSpeed = 2.5f;
        [SerializeField] private float _playerHoldingFoodAgitationSpeed = 10f;


        private protected override void OnEnter()
        {
            CurrentAgitation = 0f;
            base.OnEnter();
        }

        private protected override void OnTick()
        {
            if (_catchupState != null &&
                Vector3.Distance(_monster.transform.position, _monster.PlayerTransform.position) > CatchupDistance)
                SubStateMachine.ChangeState(_catchupState);

            CurrentAgitation += _hungryAgitationSpeed * Time.deltaTime;
            if (!_monster.PlayerPickupController.IsHandEmpty &&
                _monster.PlayerPickupController.HeldPickupItem.CompareTag("Food"))
                CurrentAgitation += _playerHoldingFoodAgitationSpeed * Time.deltaTime;

            base.OnTick();
        }

        private void OnDrawGizmos()
        {
            if (!ShouldDrawGizmos)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_monster.transform.position, CatchupDistance);
        }

        private protected override State GetTransition() =>
            CurrentAgitation > AgitateThreshold ? _onAgitationState : null;
    }
}
