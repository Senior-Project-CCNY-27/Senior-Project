using MonsterStates;
using TMPro;
using UnityEngine;

public class MonsterTimer : MonoBehaviour
{
    [SerializeField] private MonsterController monster;
    [SerializeField] private TMP_Text timer;

    // Update is called once per frame
    void Update()
    {
        if (monster == null || monster.CalmSuperState == null) return;
        timer.text = $"MonsterAgitation: {monster.CalmSuperState.TimeWithoutFood}s";
    }
}
