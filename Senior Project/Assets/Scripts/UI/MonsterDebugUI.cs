using MonsterStates;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MonsterDebugUI : MonoBehaviour
{
    [SerializeField, Required] private MonsterController _monster;
    [SerializeField, Required] private TMP_Text _displayText;
    private StateMachine _stateMachine;

    private void Awake()
    {
        if (_monster != null)
            _stateMachine = _monster.GetComponent<StateMachine>();
    }

    private void Update()
    {
        if (_monster == null || _stateMachine == null)
            return;

        if (_stateMachine.TryGetRunningState(out MonsterCalmSuperState calm))
        {
            string currentAgitation = CustomUtils.FloatToString(calm.CurrentAgitation, 1);
            string agitationThreshold = CustomUtils.FloatToString(calm.AgitateThreshold, 1);
            SetDisplayText($"Agitation Meter: {currentAgitation}/{agitationThreshold}");
        }
        else if (_stateMachine.TryGetRunningState(out MonsterAgitatedSuperState agitated))
        {
            string currentAggression = CustomUtils.FloatToString(agitated.CurrentAggression, 1);
            string aggressionThreshold = CustomUtils.FloatToString(agitated.AggressiveThreshold, 1);
            SetDisplayText($"Agression Meter: {currentAggression}/{aggressionThreshold}");
        }
        else if (_stateMachine.TryGetRunningState(out MonsterChaseState chase))
        {
            SetDisplayText($"Target: {chase.TargetTransform?.name ?? "None"}");
        }
        else
        {
            SetDisplayText();
        }
    }

    private void SetDisplayText(string infoText = null)
    {
        string superState = GetSuperState();
        string prefix = $"Monster State: {superState}";

        string subState = TryGetSubState();
        if (subState != null)
            prefix += $"-{subState}";

        string displayText = infoText == null ? prefix : $"{prefix} | {infoText}";
        if (_displayText.text == displayText)
            return;

        _displayText.text = displayText;
        _displayText.ForceMeshUpdate();

        RectTransform textRect = _displayText.rectTransform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        if (textRect.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
    }

    private string GetSuperState()
    {
        return GetStateName(_stateMachine.CurrentState) ?? "None";
    }

    private static string GetStateName(State state)
    {
        if (state is TransitionState transitionState)
        {
            string destination = transitionState.DestinationState?.gameObject.name ?? "None";
            return $"TransitionState -> {destination}";
        }

        return state?.gameObject.name;
    }

    private string TryGetSubState()
    {
        State currentState = _stateMachine.CurrentState;
        if (currentState == null)
            return null;

        return currentState is SuperState superState
            ? GetStateName(superState.SubStateMachine.CurrentState)
            : null;
    }
}
