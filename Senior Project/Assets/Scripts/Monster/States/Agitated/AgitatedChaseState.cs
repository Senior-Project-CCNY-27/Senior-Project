using Animancer;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class AgitatedChaseState : State<MonsterController>
    {
        [SerializeField] private ClipTransition _animationClip;

        [SerializeField] private float _attackDistance = 1f;
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _rotateSpeed = 10f;
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);
        }

        private protected override void OnExit()
        {
            
        }

        private protected override void OnUpdate()
        {
            _context.GraduallyLookAt(_context.PlayerTransform.position, _rotateSpeed);
        }

        private protected override void OnFixedUpdate()
        {
            _context.MoveTowards(_context.PlayerTransform.position, _moveSpeed);
        }
        
        private protected override State<MonsterController> GetTransition()
        {
            if (Vector3.Distance(_context.transform.position, _context.PlayerTransform.position) <= _attackDistance)
            {
                return _context.AgitatedSuperState.AttackState;
            }
            
            return null;
        }
    }
}