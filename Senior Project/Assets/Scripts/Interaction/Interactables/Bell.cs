using System;
using Animancer;
using MonsterStates;
using Sirenix.OdinInspector;
using UnityEngine;

public class Bell : MonoBehaviour
{
    //I kept the use count private because I dont know if we plan to have like a repair or replenishing use feature yet.
    [SerializeField]int _useCount = 2;
    [SerializeField]float _useDelay = 5;
    [SerializeField] bool _canUse = true;
    /*I had to directly reference the monster in scene since 
     I dont see a viable way to reference it in script*/
   [SerializeField,Required] StateMachine _stateMachine;
   [SerializeField] TransitionState _onMonsterCalmState;

    [Button("Ring",ButtonSizes.Medium)]
    public void Ring()
    {
        if(_useCount > 0)
        {
            _canUse = false;
            //The only way I saw to calm the monster down was by feeding it, 
            // so if there is a better way, you can replace that function with the better one
            _stateMachine.ChangeState(_onMonsterCalmState);
            _useCount --;
        }
    }
    //not the best way to go about making a cooldown, but it works
    void Update()
    {
        if(!_canUse)
        {
            _useDelay -= Time.deltaTime;
        }
        if(_useDelay <= 0)
        {
            _canUse = true;
            _useDelay = 5;
        }
    }
}
