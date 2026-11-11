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
public class MonsterController : MonoBehaviour
{
    [field: Header("References")]
    [field: SerializeField, Required] public Transform LosOrigin { get; private set; }

    [Header("Pathfinding")]
    [SerializeField, ValueDropdown(nameof(GetAgentTypes))] private int _navMeshAgentTypeId;
    [SerializeField] private Vector3 _navMeshCenterOffset;
    [SerializeField] private float _navMeshOverlapThreshold = 0.2f;
    private Vector3 _agentCenterPosition => transform.TransformPoint(_navMeshCenterOffset);
    
    public PlayerPickupController PlayerPickupController { get; private set; }
    public Transform PlayerTransform { get; private set; }
    public AnimancerComponent Animator { get; private set; }
    public Rigidbody Rigidbody { get; private set; }
    public Vector3 Destination { get; private set; }
    private List<Vector3> _path = new List<Vector3>();

    private NavMeshQueryFilter _navMeshFilter;
    
    private void Awake()
    {
        PlayerPickupController = FindFirstObjectByType<PlayerPickupController>();
        if(PlayerPickupController == null)
            Debug.LogError("Player not found");
        PlayerTransform = PlayerPickupController.transform;
        
        Animator = GetComponent<AnimancerComponent>();
        Rigidbody = GetComponent<Rigidbody>();
        
        _navMeshFilter = new NavMeshQueryFilter
        {
            agentTypeID = _navMeshAgentTypeId,
            areaMask = NavMesh.AllAreas
        };
    }
    
    private void OnDrawGizmos()
    {
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(_navMeshAgentTypeId);

        // Draws agent colliders
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_agentCenterPosition, settings.agentRadius);
        Gizmos.DrawSphere(_agentCenterPosition, 0.05f);
        Gizmos.DrawSphere(_agentCenterPosition + settings.agentHeight * Vector3.up, 0.05f);
        
        // Draws current path
        if (IsCurrentPathValid())
        {
            Gizmos.color = Color.red;
            Vector3 prevCorner = _agentCenterPosition;
            for (int i = 1; i < _path.Count; i++)
            {
                Gizmos.DrawLine(prevCorner, _path[i]);
                prevCorner = _path[i];
            }
            Gizmos.DrawSphere(_path[^1], 0.2f);
        }
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
        Rigidbody.MovePosition(transform.position + direction.WithY(0).normalized * (speed * Time.fixedDeltaTime));
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

    public void FaceTransitionTarget(GameObject target)
    {
        if (target != null)
            GraduallyLookAt(target.transform.position, 10f);
    }

    public void DestroyTransitionTarget(GameObject target)
    {
        if (target != null)
            Destroy(target);
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

    public List<GameObject> GetObjectsInConeRange(
        float forwardRadius,
        float lateralRadius,
        Vector3 origin,
        Vector3 direction,
        float halfAngle,
        LayerMask searchLayers)
    {
        List<GameObject> objectsInCone = new List<GameObject>();
        Vector3 horizontalForward = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (forwardRadius <= 0f || lateralRadius <= 0f || horizontalForward.sqrMagnitude < 0.0001f)
            return objectsInCone;

        horizontalForward.Normalize();
        Vector3 horizontalRight = Vector3.Cross(Vector3.up, horizontalForward).normalized;
        float broadphaseRadius = Mathf.Max(forwardRadius, lateralRadius);
        List<GameObject> objectsInRange = GetObjectsInRange(broadphaseRadius, origin, searchLayers);
        if (objectsInRange.Count == 0)
            return objectsInCone;

        halfAngle = Mathf.Clamp(halfAngle, 0f, 180f);
        foreach (GameObject objectInRange in objectsInRange)
        {
            Vector3 offset = Vector3.ProjectOnPlane(objectInRange.transform.position - origin, Vector3.up);
            float forwardDistance = Vector3.Dot(offset, horizontalForward);
            float lateralDistance = Vector3.Dot(offset, horizontalRight);

            float ellipseDistance =
                (forwardDistance * forwardDistance) / (forwardRadius * forwardRadius) +
                (lateralDistance * lateralDistance) / (lateralRadius * lateralRadius);
            if (ellipseDistance > 1f)
                continue;

            float angle = Mathf.Atan2(Mathf.Abs(lateralDistance), forwardDistance) * Mathf.Rad2Deg;
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
        // Pathfinding positions represent the agent center, not the monster pivot.
        Vector3 agentDestination = dest + transform.rotation * _navMeshCenterOffset;
        if (!NavMesh.SamplePosition(agentDestination, out NavMeshHit hit, 2f, _navMeshFilter))
            return null;

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(_agentCenterPosition, hit.position, _navMeshFilter, path))
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
        bool hitGround = Physics.Raycast(
            point + maxHeight * Vector3.up,
            Vector3.down,
            out RaycastHit raycastHit,
            Mathf.Infinity,
            LayerMask.GetMask("Ground"));

        if (hitGround)
        {
            Vector3 agentPoint = raycastHit.point + transform.rotation * _navMeshCenterOffset;
            bool isValidPoint = NavMesh.SamplePosition(agentPoint, out NavMeshHit navMeshHit, 0.5f, _navMeshFilter);
            outputPoint = isValidPoint
                ? navMeshHit.position - transform.rotation * _navMeshCenterOffset
                : transform.position;

            return isValidPoint;
        }

        outputPoint = transform.position;
        return false;
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

        Vector3 currentDestination = _path[1];
        Vector3 direction = (currentDestination - _agentCenterPosition).WithY(0).normalized;

        MoveInDirection(direction, moveSpeed);

        // If close to or facing in the complete opposite direction towards current waypoint, move onto the next one
        if (Vector3.Dot(direction, (_path[1] - _path[0]).normalized) < 0 || 
            Vector3.Distance(_agentCenterPosition, currentDestination) <= _navMeshOverlapThreshold)
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

    private static IEnumerable<ValueDropdownItem<int>> GetAgentTypes()
    {
        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            var settings = NavMesh.GetSettingsByIndex(i);
            int id = settings.agentTypeID;
            string name = NavMesh.GetSettingsNameFromID(id);

            yield return new ValueDropdownItem<int>(name, id);
        }
    }
    
    [Button("Open Agent ID Settings", ButtonSizes.Medium)]
    private void OpenAgentIdSettings()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExecuteMenuItem("Window/AI/Navigation");
#endif
    }
}
