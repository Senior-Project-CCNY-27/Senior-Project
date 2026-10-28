using Animancer;
using NaughtyAttributes;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class CalmSuperState : State<MonsterController>
    {
        [field: SerializeReference, ReadOnly] public StateMachine<MonsterController> SubStateMachine { get; private set; }
        [field: SerializeField] public CalmIdleState IdleState { get; private set; }
        [field: SerializeField] public CalmFollowState FollowState { get; private set; }

        [field: Header("Config")]
        [field: SerializeField] public float FollowStopDistance { get; private set; }  = 1.5f;
        [field: SerializeField] public float MaxTimeWithoutFood { get; private set; }  = 10f;
        
        private float _timeWithoutFood;

        public float TimeWithoutFood => _timeWithoutFood;

        private protected override void OnInit()
        {
            SubStateMachine = new StateMachine<MonsterController>(_context);
        
            IdleState.Init(SubStateMachine, _context);
            FollowState.Init(SubStateMachine, _context);
        }

        private protected override void OnEnter()
        {
            _timeWithoutFood = 0f;
            
            SubStateMachine.ChangeState(IdleState);
        }

        private protected override void OnExit()
        {
            SubStateMachine.CurrentState?.Exit();
        }

        private protected override void OnUpdate()
        {
            _timeWithoutFood += Time.deltaTime;
            
            SubStateMachine.Update();
        }

        private protected override void OnFixedUpdate()
        {
            SubStateMachine.FixedUpdate();
        }

        public override void OnDrawGizmosInPlayMode()
        {
            SubStateMachine.CurrentState?.OnDrawGizmosInPlayMode();
        }

        public override void OnCollisionEnter(Collision collision)
        {
            SubStateMachine.CurrentState?.OnCollisionEnter(collision);
        }

        private protected override State<MonsterController> GetTransition()
        {
            if (_timeWithoutFood > MaxTimeWithoutFood)
            {
                return new TransitionState<MonsterController>(
                    _stateMachine,
                    _context,
                    _context.Animator,
                    _context.AgitatedSuperState.AgitatedAnimationClip,
                    _context.AgitatedSuperState
                    );
            }
            
            return null;
        }
    }
}