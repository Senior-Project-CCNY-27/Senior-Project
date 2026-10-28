using System;
using System.Collections.Generic;
using System.Linq;
using Animancer;
using MonsterStates;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(AnimancerComponent))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterController : MonoBehaviour
{
    private const float NavMeshOverlapThreshold = 0.2f;
    
    [field: SerializeReference, ReadOnly] public StateMachine<MonsterController> StateMachine { get; private set; }
    [field: SerializeField] public CalmSuperState CalmSuperState { get; private set; }
    [field: SerializeField] public AgitatedSuperState AgitatedSuperState { get; private set; }

    [field: Header("Blackboard")]
    [field: SerializeField, Required] public Transform PlayerTransform { get; private set; }
    [field: SerializeField, Required] public Transform LOSOrigin { get; private set; }
    [field: SerializeField, Required] public ClipTransition EatingAnimationClip { get; private set; }
    
    public AnimancerComponent Animator { get; private set; }
    public Rigidbody Rigidbody { get; private set; }
    public NavMeshAgent NavMeshAgent { get; private set; }
    public Vector3 Destination { get; private set; }
    private List<Vector3> _path = new List<Vector3>();

    private void Awake()
    {
        Animator = GetComponent<AnimancerComponent>();
        Rigidbody = GetComponent<Rigidbody>();
        NavMeshAgent = GetComponent<NavMeshAgent>();
        
        InitStateMachine();
    }

    private void Start()
    {
        NavMeshAgent.updatePosition = false;
        NavMeshAgent.updateRotation = false;
    }

    private void OnDrawGizmos()
    {
        if (!Application.IsPlaying(this))
            return;
        
        StateMachine.CurrentState?.OnDrawGizmosInPlayMode();
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

    private void OnCollisionEnter(Collision other)
    {
        StateMachine.CurrentState?.OnCollisionEnter(other);
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
        Rigidbody.MovePosition(transform.position + direction.WithY(0).normalized * speed * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Call this in Update.
    /// </summary>
    public void GraduallyLookAt(Vector3 target, float speed)
    {
        Vector3 lookDirection = target - transform.position;
        lookDirection.y = 0;

        if (lookDirection == Vector3.zero)
            return;
        
        lookDirection.Normalize();
        
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * speed);
    }

    [Button("Agitate", EButtonEnableMode.Playmode)]
    public void Agitate()
    {
        StateMachine.ChangeState(AgitatedSuperState);
    }
    
    [Button("Feed", EButtonEnableMode.Playmode)]
    public void Feed(GameObject foodObject = null)
    {
        if(foodObject != null && foodObject.TryGetComponent(out Interactable interactable))
            interactable.EnableInteractability(false);
        
        StateMachine.ChangeState(new TransitionState<MonsterController>(
            StateMachine,
            this,
            Animator,
            EatingAnimationClip,
            CalmSuperState,
            onExitCallback: foodObject ==null ? null : () =>
            {
                if(foodObject != null)
                    Destroy(foodObject);
            }
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
            if(hit.transform.IsChildOf(gameObject.transform))
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
    
    public List<Vector3> GetPathToDestination(Vector3 dest)
    {
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = NavMeshAgent.agentTypeID,
            areaMask = NavMeshAgent.areaMask
        };

        // Sample using the same agent type and allowed areas.
        if (!NavMesh.SamplePosition(dest, out NavMeshHit hit, 2f, filter))
            return null;

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(transform.position, hit.position, filter, path))
            return null;

        if (path.status != NavMeshPathStatus.PathComplete)
            return null;

        return path.corners.ToList();
    }
    
    /// <summary>
    /// Sets the destination for the monster to move towards.
    /// </summary>
    /// <param name="dest">The destination position.</param>
    public void SetDestination(Vector3 dest)
    {
        Destination = dest;
        _path = GetPathToDestination(dest);
    }
    
    /// <summary>
    /// Cancels the current path of the monster.
    /// </summary>
    public void CancelPath()
    {
        _path = null;
    }
    
    /// <summary>
    /// Checks if the current path of the monster is valid.
    /// </summary>
    /// <returns>True if the path is valid, false otherwise.</returns>
    public bool IsCurrentPathValid()
    {
        if (_path == null) 
            return false;
        
        if (_path.Count < 2) 
            return false;

        return true;
    }
    
    /// <summary>
    /// Moves the enemy towards its destination along the calculated path.
    /// Call this in FixedUpdate
    /// </summary>
    public void MoveTowardsDestination(float moveSpeed)
    {
        if (_path == null)
        {
            Debug.LogWarning($"{gameObject.name} has no path to follow. Make sure you handle this using bool IsCurrentPathValid().");
            return;
        }
        
        if (_path.Count < 2)
        {
            Debug.LogWarning($"{gameObject.name} has a path with only 1 waypoint. Make sure you handle this bool IsCurrentPathValid().");
            return;
        }

        #region Debug
        Vector3 prevCorner = transform.position;
        for (int i = 1; i < _path.Count; i++)
        {
            Debug.DrawLine(prevCorner, _path[i], Color.green);
            prevCorner = _path[i];
        }
        #endregion

        Vector3 currentDestination = _path[1];
        Vector3 direction = (currentDestination - transform.position).WithY(0).normalized;

        MoveInDirection(direction, moveSpeed);
        NavMeshAgent.nextPosition = transform.position;

        // If close to or facing in the complete opposite direction towards current waypoint, move onto the next one
        if (Vector3.Dot(direction, (_path[1] - _path[0]).normalized) < 0 || Vector3.Distance(transform.position, currentDestination) <= NavMeshOverlapThreshold)
            _path.RemoveAt(0);
    }
    
    /// <summary>
    /// Call this in Update
    /// </summary>
    public void RotateTowardsDestination(float rotateSpeed)
    {
        if (!IsCurrentPathValid())
            return;
        
        Vector3 currentDestination = _path[1];
        GraduallyLookAt(currentDestination, rotateSpeed);
    }
}