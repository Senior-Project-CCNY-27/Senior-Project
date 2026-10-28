using MonsterStates;
using NaughtyAttributes;
using TMPro;
using UnityEngine;

public class MonsterTimer : MonoBehaviour
{
    [SerializeField, Required] private MonsterController _monster;
    [SerializeField, Required] private TMP_Text _timerText;
    
    void Update()
    {
        if (_monster == null || _monster.CalmSuperState == null) 
            return;

        string currentTime = CustomUtils.FloatToString(_monster.CalmSuperState.TimeWithoutFood);
        string maxTime = CustomUtils.FloatToString(_monster.CalmSuperState.MaxTimeWithoutFood);
        _timerText.text = $"MonsterAgitation: {currentTime}s/{maxTime}s";
    }
}
