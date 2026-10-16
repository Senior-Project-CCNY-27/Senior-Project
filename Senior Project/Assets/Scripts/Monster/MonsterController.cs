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
    [field: SerializeField] public Transform LOSOrigin { get; private set; }
    [field: SerializeField] public ClipTransition EatingAnimationClip { get; private set; }
    
    public AnimancerComponent Animator { get; private set; }
    public Rigidbody Rigidbody { get; private set; }

    private void Awake()
    {
        Animator = GetComponent<AnimancerComponent>();
        Rigidbody = GetComponent<Rigidbody>();
        
        InitStateMachine();
    }

    private void OnDrawGizmos()
    {
        if (!Application.IsPlaying(this))
            return;
        
        CalmSuperState?.OnDrawGizmosInPlayMode();
        AgitatedSuperState?.OnDrawGizmosInPlayMode();
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

    public List<GameObject> GetObjectsInRange(float radius, Vector3 origin, LayerMask searchLayers)
    {
        List<GameObject> objectsInRange = new List<GameObject>();
        Collider[] hits = Physics.OverlapSphere(origin, radius, searchLayers);
        if (hits == null || hits.Length == 0)
            return objectsInRange;
        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject)
                continue;
            objectsInRange.Add(hit.gameObject);
        }

        return objectsInRange;
    } 

    public List<GameObject> GetObjectsInConeRange(float radius, Vector3 origin, Vector3 direction, float halfAngle, LayerMask searchLayers)
    {
        List<GameObject> objectsInCone = new List<GameObject>();
        List<GameObject> objectsInRange = GetObjectsInRange(radius, origin, searchLayers);
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

    public List<GameObject> GetVisibleObjects(List<GameObject> objects, Vector3 origin, float radius, LayerMask searchLayers, LayerMask wallLayers)
    {
        List<GameObject> visibleObjects = new List<GameObject>();
        if (objects.Count == 0) 
            return visibleObjects;
        
        foreach (GameObject obj in objects)
        {
            Vector3 directionToTarget = obj.transform.position - origin;
            if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, radius , searchLayers | wallLayers))
            {
                if (wallLayers.Contains(hit.collider.gameObject.layer))
                    continue;
            }
            visibleObjects.Add(obj);
        }
        return visibleObjects;
    }
}