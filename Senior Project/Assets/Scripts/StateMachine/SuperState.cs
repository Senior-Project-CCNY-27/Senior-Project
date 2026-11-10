using UnityEngine;
// Add a StateMachine to this GameObject and put its states on direct children.
[RequireComponent(typeof(StateMachine))]
public class SuperState : State
{
    public StateMachine SubStateMachine { get; private set; }

    private protected override void OnInit()
    {
        SubStateMachine = GetComponent<StateMachine>();
        SubStateMachine.DriveExternally();
    }

    private protected override void OnEnter() =>
        SubStateMachine.StartMachine();

    private protected override void OnExit() =>
        SubStateMachine.StopMachine();

    private protected override void OnTick() =>
        SubStateMachine.Tick();

    private protected override void OnFixedTick() =>
        SubStateMachine.FixedTick();

    public override void HandleCollision(Collision collision) =>
        SubStateMachine.CurrentState?.HandleCollision(collision);
}
