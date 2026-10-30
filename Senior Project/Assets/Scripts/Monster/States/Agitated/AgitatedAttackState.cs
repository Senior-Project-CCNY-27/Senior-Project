using Animancer;
using NaughtyAttributes;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class AgitatedAttackState : State<MonsterController>
    {
        [SerializeField] private ClipTransition _animationClip;
        [SerializeField] private int _damage = 1;
        [SerializeField, Required] private StringAsset _damageEventName;
        
        [Header("Mouth Hitbox")]
        [SerializeField] private float _attackRadius = 1f;
        
        [Header("Attack Camera Shake")]
        [SerializeField] private CameraShakeSettings _onHitShakeSettings;

        private float _duration;
        private float _timer;
        
        private protected override void OnEnter()
        {
            _animationClip.Events.SetCallback(_damageEventName, TryDamageTarget);
            
            _context.Animator.Play(_animationClip, 0.1f);
            
            _duration = _animationClip.Length / _animationClip.Speed;
            _timer = 0f;
        }

        private protected override void OnExit()
        {
            _animationClip.Events.RemoveCallback(_damageEventName, TryDamageTarget);
        }

        private protected override void OnUpdate()
        {
            _timer += Time.deltaTime;
        }

        private protected override void OnFixedUpdate()
        {
            
        }

        private protected override State<MonsterController> GetTransition()
        {
            if (_timer >= _duration)
            {
                return _context.AgitatedSuperState.ChaseState;
            }
            
            return null;
        }

        private void TryDamageTarget()
        {
            CustomDebug.InstantiateTemporarySphere(
                _context.AgitatedSuperState.MouthHitboxTransform.position,
                _attackRadius,
                1f,
                Color.red.WithA(0.5f)
                );
            
            Collider[] mouthCollisions = Physics.OverlapSphere(_context.AgitatedSuperState.MouthHitboxTransform.position, _attackRadius);
            foreach (Collider mouthCollision in mouthCollisions)
            {
                Health health = mouthCollision.GetComponentInParent<Health>();
                if(health == null)
                    continue;
                
                CameraShakeManager.ShakeCamera(_onHitShakeSettings);
                health.TakeDamage(_damage);
            }
        }
        
        public override void OnDrawGizmosInPlayMode()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_context.AgitatedSuperState.MouthHitboxTransform.position, _attackRadius);
        }
    }
}