using Animancer;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class CalmIdleState : State<MonsterController>
    {
        [SerializeField] private ClipTransition _animationClip;
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);
        }

        private protected override void OnExit()
        {
            
        }

        private protected override void OnUpdate()
        {
            
        }

        private protected override void OnFixedUpdate()
        {
            
        }

        private protected override State<MonsterController> GetTransition()
        {
            if (Vector3.Distance(_context.transform.position, _context.PlayerTransform.position) > _context.CalmSuperState.FollowStopDistance)
            {
                return _context.CalmSuperState.FollowState;
            }
            
            return null;
        }
    }
}