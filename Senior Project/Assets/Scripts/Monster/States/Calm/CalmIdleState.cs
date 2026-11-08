using Animancer;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class CalmIdleState : State<MonsterController>
    {
        [SerializeField] private ClipTransition _animationClip;
        [SerializeField] private FloatRange _durationRange = new FloatRange(1f, 1.5f);

        private float _timer;
        private float _duration;
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);

            _timer = 0f;
            _duration = _durationRange.RandomValue();
        }

        private protected override void OnExit()
        {
            
        }

        private protected override void OnUpdate()
        {
            _timer += Time.deltaTime;
        }

        private protected override void OnFixedUpdate()
        {
            
        }

        public override void OnDrawGizmosInPlayMode()
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                _context.transform.position + Vector3.up * 2f,
                $"Idle: {Mathf.Max(0f, _duration - _timer):0.0}s");
#endif
        }

        private protected override State<MonsterController> GetTransition()
        {
            if (_timer >= _duration)
                return _context.CalmSuperState.FollowState;
            
            return null;
        }
    }
}
