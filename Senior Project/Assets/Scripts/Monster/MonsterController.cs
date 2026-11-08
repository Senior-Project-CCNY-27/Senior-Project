using System;
using System.Collections.Generic;
using System.Linq;
using Animancer;
using MonsterStates;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

[RequireComponent(typeof(AnimancerComponent))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterController : MonoBehaviour
{
    private const float NavMeshOverlapThreshold = 0.2f;
    
    public StateMachine<MonsterController> StateMachine { get; private set; }
    [field: SerializeField] public CalmSuperState CalmSuperState { get; private set; }
    [field: SerializeField] public AggressiveSuperState AggressiveSuperState { get; private set; }

    [field: Header("Blackboard")]
    [field: SerializeField, Required] public Transform PlayerTransform { get; private set; }
    [field: SerializeField, Required] public PlayerPickupController PlayerPickupController { get; private set; }
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
        AggressiveSuperState.Init(StateMachine, this);
        
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

    [Button("Force Aggressive", ButtonSizes.Large)]
    public void ForceAggressive()
    {
        StateMachine.ChangeState(
            new TransitionState<MonsterController>(
                StateMachine,
                this,
                Animator,
                AggressiveSuperState.RoarAnimationClip,
                AggressiveSuperState
        ));
    }
    
    [Button("Feed", ButtonSizes.Large)]
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
            },
            onUpdateCallback: foodObject == null ? null : () =>
            {
                if(foodObject != null)
                    GraduallyLookAt(foodObject.transform.position, 10f);
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

    public List<GameObject> GetVisibleObjects(List<GameObject> objects)
    {
        List<GameObject> visibleObjects = new List<GameObject>();
        if (objects.Count == 0) 
            return visibleObjects;
        
        foreach (GameObject obj in objects)
        {
            if (IsPositionBlockedByWall(obj.transform.position))
                continue;

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
    /// Checks if a given point is on the NavMesh using raycast from maxHeight above the point.
    /// Also returns the valid point on the NavMesh or the enemy's current position if not valid.
    /// The y value doesn't matter, and the point will always be checked at the navmesh ground level.
    /// </summary>
    /// <param name="point">The point to check.</param>
    /// <param name="maxHeight">The max height above the point to start raycasting down from.</param>
    /// <param name="outputPoint">The output point.</param>
    /// <returns>True if the point is on the NavMesh, false otherwise.</returns>
    public bool IsValidPointOnNavMesh(Vector3 point, float maxHeight, out Vector3 outputPoint)
    {
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = NavMeshAgent.agentTypeID,
            areaMask = NavMeshAgent.areaMask
        };

        RaycastHit raycastHit;
        bool isValidPoint =
            Physics.Raycast(point + maxHeight * Vector3.up, Vector3.down, out raycastHit, Mathf.Infinity, LayerMask.GetMask("Ground"))
            && NavMesh.SamplePosition(raycastHit.point, out _, 0.5f, filter);

        outputPoint = isValidPoint ? raycastHit.point : transform.position;

        return isValidPoint;
    }

    /// <summary>
    /// Checks whether a wall blocks the line from the monster to a position.
    /// </summary>
    public bool IsPositionBlockedByWall(Vector3 position)
    {
        Vector3 direction = position - transform.position;
        return Physics.Raycast(transform.position, direction, direction.magnitude, LayerMask.GetMask("Wall"), QueryTriggerInteraction.Ignore);
    }
    
    /// <summary>
    /// Generates a random wander point between inner and outer radii about a center position.
    /// Returns itself it cannot find a valid point after a certain number of iterations.
    /// Iteration count is set to 16 by default.
    /// </summary>
    /// <param name="center">The center position for generating the wander point.</param>
    /// <param name="wanderRadiusBounds">The inner and outer radius bounds.</param>
    /// <param name="iterationTryCount">The number of iterations to try finding a valid point.</param>
    /// <returns>The randomly generated wander point.</returns>
    public Vector3 GetRandomWanderPoint(Vector3 center, FloatRange wanderRadiusBounds, int iterationTryCount = 16)
    {
        return FindRandomWanderPoint(center, wanderRadiusBounds, iterationTryCount, false);
    }

    /// <summary>
    /// Generates a random NavMesh wander point that is visible from the monster.
    /// Returns the monster's position if no visible point is found.
    /// </summary>
    public Vector3 GetRandomVisibleWanderPoint(Vector3 center, FloatRange wanderRadiusBounds, int iterationTryCount = 16)
    {
        return FindRandomWanderPoint(center, wanderRadiusBounds, iterationTryCount, true);
    }

    private Vector3 FindRandomWanderPoint(Vector3 center, FloatRange wanderRadiusBounds, int iterationTryCount, bool requireVisibility)
    {
        float innerRadius = Mathf.Max(0f, wanderRadiusBounds.Min);
        float outerRadius = wanderRadiusBounds.Max;
        if (outerRadius <= innerRadius)
            return transform.position;

        float innerRadiusSquared = innerRadius * innerRadius;
        float outerRadiusSquared = outerRadius * outerRadius;

        for (int i = 0; i < iterationTryCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * outerRadius;
            if (offset.sqrMagnitude < innerRadiusSquared)
                continue;

            Vector3 randomPoint = center + new Vector3(offset.x, 0f, offset.y);

            if (!IsValidPointOnNavMesh(randomPoint, 100f, out Vector3 validPoint))
                continue;

            float distanceSquared = (validPoint - center).WithY(0).sqrMagnitude;
            if (distanceSquared >= innerRadiusSquared
                && distanceSquared <= outerRadiusSquared
                && (!requireVisibility || !IsPositionBlockedByWall(validPoint)))
                return validPoint;
        }

        return transform.position;
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
