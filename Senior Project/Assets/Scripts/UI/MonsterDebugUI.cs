using System.Reflection;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MonsterDebugUI : MonoBehaviour
{
    [SerializeField, Required] private MonsterController _monster;
    [SerializeField, Required] private TMP_Text _displayText;

    private void Update()
    {
        if (_monster == null)
            return;

        if (_monster.StateMachine.CurrentState == _monster.CalmSuperState)
        {
            string currentTime = CustomUtils.FloatToString(_monster.CalmSuperState.CurrentAgitation);
            string maxTime = CustomUtils.FloatToString(_monster.CalmSuperState.AgitateThreshold);
            SetDisplayText($"Time to agitation: {currentTime}s/{maxTime}s");
        }
        else if (_monster.StateMachine.CurrentState == _monster.AgitatedSuperState &&
                 _monster.AgitatedSuperState.SubStateMachine.CurrentState == _monster.AgitatedSuperState.ChaseState)
        {
            SetDisplayText($"Target: {_monster.AgitatedSuperState.ChaseState.TargetTransform.name}");
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
        return GetStateName(_monster.StateMachine.CurrentState) ?? "None";
    }

    private static string GetStateName(State<MonsterController> state)
    {
        if (state is TransitionState<MonsterController> transitionState)
        {
            string destination = transitionState.DestinationState?.GetType().Name ?? "None";
            return $"TransitionState -> {destination}";
        }

        return state?.GetType().Name;
    }

    private string TryGetSubState()
    {
        State<MonsterController> currentState = _monster.StateMachine.CurrentState;
        if (currentState == null)
            return null;

        PropertyInfo subStateMachinePropertyInfo =
            currentState.GetType().GetProperty(
                "SubStateMachine",
                BindingFlags.Instance | BindingFlags.Public
                );

        if (subStateMachinePropertyInfo == null)
            return null;

        StateMachine<MonsterController> subStateMachine = subStateMachinePropertyInfo.GetValue(currentState) as StateMachine<MonsterController>;
        if (subStateMachine == null)
            return null;

        return GetStateName(subStateMachine.CurrentState);
    }
}
