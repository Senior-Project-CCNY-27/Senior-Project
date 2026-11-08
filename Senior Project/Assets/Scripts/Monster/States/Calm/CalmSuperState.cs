using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class CalmSuperState : State<MonsterController>
    {
        public StateMachine<MonsterController> SubStateMachine { get; private set; }
        [field: SerializeField] public CalmIdleState IdleState { get; private set; }
        [field: SerializeField] public CalmFollowState FollowState { get; private set; }
        [field: SerializeField] public CalmWanderState WanderState { get; private set; }
        [field: SerializeField] public CalmCatchupState CatchupState { get; private set; }

        [field: Header("Movement")]
        [field: SerializeField] public float FollowStopDistance { get; private set; } = 2.5f;
        [field: SerializeField] public float CatchupDistance { get; private set; } = 10f;

        [field: Header("Agitation")]
        [field: SerializeField] public float AgitateThreshold { get; private set; } = 100f;
        [field: SerializeField, ReadOnly] public float CurrentAgitation { get; private set; }
        [SerializeField] private float _hungryAgitationSpeed = 5f;
        [SerializeField] private float _playerHoldingFoodAgitationSpeed = 25f;

        private protected override void OnInit()
        {
            SubStateMachine = new StateMachine<MonsterController>(_context);
        
            IdleState.Init(SubStateMachine, _context);
            FollowState.Init(SubStateMachine, _context);
            WanderState.Init(SubStateMachine, _context);
            CatchupState.Init(SubStateMachine, _context);
        }

        private protected override void OnEnter()
        {
            CurrentAgitation = 0f;
            
            SubStateMachine.ChangeState(FollowState, true);
        }

        private protected override void OnExit()
        {
            SubStateMachine.CurrentState?.Exit();
        }

        private protected override void OnUpdate()
        {
            HandleCatchup();
            HandleAgitationBuildup();
            
            SubStateMachine.Update();
        }

        private protected override void OnFixedUpdate()
        {
            SubStateMachine.FixedUpdate();
        }

        public override void OnDrawGizmosInPlayMode()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_context.transform.position, CatchupDistance);

            SubStateMachine.CurrentState?.OnDrawGizmosInPlayMode();
        }

        public override void OnCollisionEnter(Collision collision)
        {
            SubStateMachine.CurrentState?.OnCollisionEnter(collision);
        }

        private protected override State<MonsterController> GetTransition()
        {
            if (CurrentAgitation > AgitateThreshold)
            {
                _context.Agitate();
                return null;
            }
            
            return null;
        }

        private void HandleAgitationBuildup()
        {
            HandleHungryAgitation();
            HandlePlayerHoldingFoodAgitation();
        }
        
        private void HandleHungryAgitation()
        {
            CurrentAgitation += _hungryAgitationSpeed * Time.deltaTime;
        }

        private void HandlePlayerHoldingFoodAgitation()
        {
            if (_context.PlayerPickupController.IsHandEmpty)
                return;

            if (!_context.PlayerPickupController.HeldPickupItem.CompareTag("Food"))
                return;
            
            CurrentAgitation += _playerHoldingFoodAgitationSpeed * Time.deltaTime;
        }

        private void HandleCatchup()
        {
            if (Vector3.Distance(_context.transform.position, _context.PlayerTransform.position) > CatchupDistance)
                SubStateMachine.ChangeState(CatchupState);
        }
    }
}
