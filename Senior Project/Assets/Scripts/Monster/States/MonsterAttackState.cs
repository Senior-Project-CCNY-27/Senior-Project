using System.Collections.Generic;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MonsterStates
{
    public class MonsterAttackState : State
    {
        [Header("References")]
        [SerializeField, Required] private MonsterController _monster;

        [Header("Transitions")]
        [SerializeField] private State _onFinishedState;

        [Header("Animation")]
        [SerializeField] private ClipTransition _animationClip;

        [Header("Damage")]
        [SerializeField] private int _damage = 1;
        [SerializeField, Required] private StringAsset _damageEventName;
        
        [Header("Mouth Hitbox")]
        [SerializeField] private float _attackRadius = 1f;
        [SerializeField] private Transform _mouthHitboxTransform;
        
        [Header("Attack Camera Shake")]
        [SerializeField] private CameraShakeSettings _onHitShakeSettings;

        private float _duration;
        private float _timer;
        
        private protected override void OnEnter()
        {
            _animationClip.Events.SetCallback(_damageEventName, TryDamageTarget);
            
            _monster.Animator.Play(_animationClip, 0.1f);
            
            _duration = _animationClip.Length / _animationClip.Speed;
            _timer = 0f;
        }

        private protected override void OnExit()
        {
            _animationClip.Events.RemoveCallback(_damageEventName, TryDamageTarget);
        }

        private protected override void OnTick()
        {
            _timer += Time.deltaTime;
        }

        private protected override void OnFixedTick()
        {
            
        }

        private protected override State GetTransition()
        {
            if (_timer >= _duration)
            {
                return _onFinishedState;
            }
            
            return null;
        }

        private void TryDamageTarget()
        {
            CustomDebug.InstantiateTemporarySphere(
                _mouthHitboxTransform.position,
                _attackRadius,
                1f,
                Color.red.WithA(0.5f)
                );
            
            Collider[] mouthCollisions = Physics.OverlapSphere(_mouthHitboxTransform.position, _attackRadius);
            HashSet<Health> damagedHealths = new HashSet<Health>();
            foreach (Collider mouthCollision in mouthCollisions)
            {
                Health health = mouthCollision.GetComponentInParent<Health>();
                if(health == null)
                    continue;

                if (damagedHealths.Contains(health))
                    continue;
                
                damagedHealths.Add(health);
                CameraShakeManager.ShakeCamera(_onHitShakeSettings);
                health.TakeDamage(_damage);
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!ShouldDrawGizmos)
                return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_mouthHitboxTransform.position, _attackRadius);
        }
    }
}
