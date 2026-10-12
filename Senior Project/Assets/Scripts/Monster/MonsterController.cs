using System;
using Animancer;
using MonsterStates;
using NaughtyAttributes;
using UnityEngine;

[RequireComponent(typeof(AnimancerComponent))]
[RequireComponent(typeof(Rigidbody))]
public class MonsterController : MonoBehaviour
{
    [field: SerializeReference, ReadOnly] public StateMachine<MonsterController> StateMachine { get; private set; }
    [field: SerializeField] public CalmSuperState CalmSuperState { get; private set; }
    [field: SerializeField] public AgitatedSuperState AgitatedSuperState { get; private set; }

    [field: Header("Config")]
    [field: SerializeField] public Transform PlayerTransform { get; private set; }
    [field: SerializeField] public ClipTransition EatingAnimationClip { get; private set; }
    public AnimancerComponent Animator { get; private set; }
    public Rigidbody Rigidbody { get; private set; }
    
    private void Awake()
    {
        Animator = GetComponent<AnimancerComponent>();
        Rigidbody = GetComponent<Rigidbody>();
        
        InitStateMachine();
    }

    private void Update()
    {
        StateMachine.Update();
    }

    private void FixedUpdate()
    {
        StateMachine.FixedUpdate();
    }

    private void OnDestroy()
    {
        StateMachine.Destroy();
    }

    private void InitStateMachine()
    {
        StateMachine = new StateMachine<MonsterController>(this);
        
        CalmSuperState.Init(StateMachine, this);
        AgitatedSuperState.Init(StateMachine, this);
        
        StateMachine.ChangeState(CalmSuperState);
    }
    
    /// <summary>
    /// Call this in FixedUpdate.
    /// </summary>
    public void MoveTowards(Vector3 destination, float speed)
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0;
        direction.Normalize();

        MoveInDirection(direction, speed);
    }

    /// <summary>
    /// Call this in FixedUpdate.
    /// </summary>
    public void MoveInDirection(Vector3 direction, float speed)
    {
        Rigidbody.MovePosition(transform.position + direction.normalized * speed * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Call this in Update.
    /// </summary>
    public void GraduallyLookAt(Vector3 target, float speed)
    {
        Vector3 lookDirection = target - transform.position;
        lookDirection.y = 0;
        lookDirection.Normalize();
        
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * speed);
    }

    [Button("Feed", EButtonEnableMode.Playmode)]
    public void Feed()
    {
        StateMachine.ChangeState(new TransitionState<MonsterController>(
            StateMachine,
            this,
            Animator,
            EatingAnimationClip,
            CalmSuperState
        ));
    }
}