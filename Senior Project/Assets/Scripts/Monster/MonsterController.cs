using System;
using System.Collections.Generic;
using Animancer;
using MonsterStates;
using NaughtyAttributes;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

[RequireComponent(typeof(AnimancerComponent))]
[RequireComponent(typeof(Rigidbody))]
public class MonsterController : MonoBehaviour
{
    [field: SerializeReference, ReadOnly] public StateMachine<MonsterController> StateMachine { get; private set; }
    [field: SerializeField] public CalmSuperState CalmSuperState { get; private set; }
    [field: SerializeField] public AgitatedSuperState AgitatedSuperState { get; private set; }

    [field: Header("Config")]
    [field: SerializeField] public Transform PlayerTransform { get; private set; }
     public ClipTransition EatingAnimationClip { get; private set; }
    public AnimancerComponent Animator { get; private set; }
    public Rigidbody Rigidbody { get; private set; }
    public CapsuleCollider Collider { get; private set; }
    [field: SerializeField] public Transform LOSOrigin { get; private set; }

    private void Awake()
    {
        Animator = GetComponent<AnimancerComponent>();
        Rigidbody = GetComponent<Rigidbody>();
        Collider = GetComponent<CapsuleCollider>();

        
        InitStateMachine();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = GetVisibleObjectsInConeRange(AgitatedSuperState.RoamState._range, LayerMask.GetMask("Player"), LOSOrigin.position, transform.forward, AgitatedSuperState.RoamState._halfAngleCone, LayerMask.GetMask("Wall")).Count > 0 ? Color.red: Color.green; 
        CustomDebug.DrawSphereWireCone(LOSOrigin.position, transform.forward, AgitatedSuperState.RoamState._halfAngleCone, AgitatedSuperState.RoamState._range);
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

    public List<GameObject> GetObjectsInRange(float radius, LayerMask layers, Vector3 origin)
    {
        List<GameObject> objectsInRange = new List<GameObject>();
        Collider[] hits = Physics.OverlapSphere(origin, radius, layers);
        if (hits == null || hits.Length == 0)
            return objectsInRange;
        foreach (Collider hit in hits)
        {
            if (hit == Collider)
                continue;
            objectsInRange.Add(hit.gameObject);
        }

        return objectsInRange;
    } 

    public List<GameObject> GetObjectsInConeRange(float radius, LayerMask layers, Vector3 origin, Vector3 direction, float halfAngle)
    {
        List<GameObject> objectsInCone = new List<GameObject>();
        List<GameObject> objectsInRange = GetObjectsInRange(radius, layers, origin);
        if (objectsInRange.Count == 0)
            return objectsInCone;
        foreach (GameObject objectInRange in objectsInRange)
        {
            Vector3 directionToTarget = objectInRange.transform.position - origin; 
            float angle = Vector3.Angle(directionToTarget.WithY(0), direction.WithY(0));
            if (angle <= halfAngle)
                objectsInCone.Add(objectInRange);
        }
        return objectsInCone;
    }

    public List<GameObject> GetVisibleObjectsInConeRange(float radius, LayerMask searchLayers, Vector3 origin, Vector3 direction, float halfAngle, LayerMask wallLayers)
    {
        List<GameObject> visbleObjectsInCone = new List<GameObject>();
        List<GameObject> objectsInCone = GetObjectsInConeRange(radius, searchLayers, origin, direction, halfAngle);
        if (objectsInCone.Count == 0) 
            return visbleObjectsInCone;
        foreach (GameObject objectInCone in objectsInCone)
        {
            Vector3 directionToTarget = objectInCone.transform.position - origin;
            if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, radius , searchLayers | wallLayers))
            {
                if (wallLayers.Contains(hit.collider.gameObject.layer))
                    continue;
            }
            visbleObjectsInCone.Add(objectInCone);
        }
        return visbleObjectsInCone;
    }
}