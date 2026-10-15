using System;
using Animancer;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class AgitatedRoamState : State<MonsterController>
    {
        [SerializeField] private ClipTransition _animationClip;

        [SerializeField] public float _range = 10;
        [SerializeField] public float _halfAngleCone = 45;

        
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
            /*if (_context.GetVisibleObjectsInConeRange(_range, LayerMask.GetMask("Player"), _context.LOSOrigin.position, _context.transform.forward, _halfAngleCone, LayerMask.GetMask("Wall")).Count > 0) 
            {
                return _context.AgitatedSuperState.ChaseState;
            }*/

            return null;
        }
    }
}