using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

public class NearbyInteractableDetector : MonoBehaviour
{
    [SerializeField, Required] private Transform _detectionOriginTransform;
    [SerializeField] private List<Collider> _nonBlockingColliders = new List<Collider>();
    [SerializeField] private float _detectionRange = 5f;
    
    [field: SerializeField, ReadOnly] public Interactable CurrentInteractable { get; private set; }
    
    [field: Header("Event")]
    [field: SerializeField] public UnityEvent<Interactable> OnInteractableDetected { get; private set; } = new();

    private Camera _mainCamera;
    private LayerMask _detectionLayer;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _detectionLayer = LayerMask.GetMask("Interactable");
    }

    private void OnDrawGizmos()
    {
        if (_detectionOriginTransform == null)
            return;
        
        Gizmos.DrawWireSphere(_detectionOriginTransform.position, _detectionRange);
    }

    private void Update()
    {
        Interactable newInteractable = GetNearbyInteractable();
        if (!ReferenceEquals(CurrentInteractable, newInteractable))
        {
            CurrentInteractable = newInteractable;
            OnInteractableDetected.Invoke(CurrentInteractable);
        }
    }

    private Interactable GetNearbyInteractable()
    {
        Collider[] nearbyObjects = Physics.OverlapSphere(_detectionOriginTransform.position, _detectionRange, _detectionLayer);
        if (nearbyObjects.Length == 0)
            return null;

        Interactable closestInteractable = null;
        float closestScreenDistance = float.PositiveInfinity;
        foreach (Collider nearbyObject in nearbyObjects)
        {
            if (!nearbyObject.TryGetComponent(out Interactable interactable))
                continue;

            if (!interactable.IsInteractionEnabled)
                continue;

            if (!CustomUtils.IsColliderOnScreen(_mainCamera, nearbyObject))
                continue;
            
            if (!HasLineOfSight(nearbyObject))
                continue;
            
            float screenDistance = CustomUtils.GetScreenDistanceFromCenter(_mainCamera, nearbyObject.bounds.center);
            if (screenDistance < closestScreenDistance)
            {
                closestScreenDistance = screenDistance;
                closestInteractable = interactable;
            }
        }
        
        return closestInteractable;
    }
    
    private bool HasLineOfSight(Collider target)
    {
        Vector3 origin = _detectionOriginTransform.position;
        Vector3 targetPoint = target.ClosestPoint(origin);
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction / distance,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == target)
                continue;

            if (_nonBlockingColliders != null &&
                _nonBlockingColliders.Contains(hit.collider))
                continue;

            return false;
        }

        return true;
    }
}