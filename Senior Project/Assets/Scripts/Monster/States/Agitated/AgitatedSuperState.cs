using NaughtyAttributes;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class AgitatedSuperState : State<MonsterController>
    {
        [field: SerializeReference, ReadOnly] public StateMachine<MonsterController> SubStateMachine { get; private set; }
        [field: SerializeField] public AgitatedRoamState RoamState { get; private set; }
        [field: SerializeField] public AgitatedChaseState ChaseState { get; private set; }
        [field: SerializeField] public AgitatedAttackState AttackState { get; private set; }
        
        private protected override void OnInit()
        {
            SubStateMachine = new StateMachine<MonsterController>(_context);
        
            RoamState.Init(SubStateMachine, _context);
            ChaseState.Init(SubStateMachine, _context);
            AttackState.Init(SubStateMachine, _context);
        }
        
        private protected override void OnEnter()
        {
            SubStateMachine.ChangeState(RoamState);
        }

        private protected override void OnExit()
        {
            
        }

        private protected override void OnUpdate()
        {
            SubStateMachine.Update();
        }

        private protected override void OnFixedUpdate()
        {
            SubStateMachine.FixedUpdate();
        }

        public override void OnDrawGizmosInPlayMode()
        {
            RoamState?.OnDrawGizmosInPlayMode();
            ChaseState?.OnDrawGizmosInPlayMode();
            AttackState?.OnDrawGizmosInPlayMode();
        }

        private protected override State<MonsterController> GetTransition()
        {
            return null;
        }
    }
}