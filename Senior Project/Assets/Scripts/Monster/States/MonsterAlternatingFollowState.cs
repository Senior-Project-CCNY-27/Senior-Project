using Sirenix.OdinInspector;
using UnityEngine;

namespace MonsterStates
{
    public class MonsterAlternatingFollowState : MonsterFollowState
    {
        [Header("Alternate Transition")]
        [SerializeField] private State _onAlternateReachedState;
        [SerializeField] private State _alternateAfterState;
        [SerializeField, Range(0f, 1f)] private float _alternateChance;

        private protected override State GetTransition()
        {
            if (!HasReachedPlayer)
                return null;

            if (_onAlternateReachedState != null &&
                (_stateMachine.PreviousState == _alternateAfterState || RandomUtils.Chance(_alternateChance)))
                return _onAlternateReachedState;

            return ReachedState;
        }
    }
}
