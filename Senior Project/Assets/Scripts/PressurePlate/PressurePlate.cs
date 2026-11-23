using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class PressurePlate : MonoBehaviour
{
    [Header("References")]
    [SerializeField, ReadOnly] private BoxCollider _boxCollider;
    
    [Header("Config")]
    [SerializeField] private float _checkYOffset = 0.15f;
    [SerializeField] private bool _requiresPlayer;
    [SerializeField] private bool _requiresMonster;
    [SerializeField, ReadOnly] private bool _isPlayerAbove;
    [SerializeField, ReadOnly] private bool _isMonsterAbove;
    
    [Header("Pressed State")]
    [SerializeField] private float _pressedYHeight = 0.05f;
    [SerializeField] private float _animationDuration = 0.5f;
    [SerializeField] private Ease _animationEase = Ease.Linear;
    
    [Header("Event")]
    [SerializeField] private UnityEvent<bool> _onPlateTriggered = new();
    [SerializeField] private UnityEvent _onPlateFullyPressed = new();

    private float _startYPosition;
    private Vector3 _overlapBoxCenter;
    private Vector3 _overlapBoxHalfExtents;
    private Quaternion _overlapBoxRotation;
    private bool _hasReportedRequirements;
    private bool _lastRequirementsMet;

    private void OnValidate()
    {
        CacheReferences();
        if (_boxCollider != null)
            CacheOverlapBox();
    }

    private void Awake()
    {
        CacheReferences();
        CacheOverlapBox();
        
        _startYPosition = transform.position.y;
    }

    private void Update()
    {
        CheckObjectsAbove();
        CheckRequirements();
    }

    private void OnDrawGizmos()
    {
        if (_boxCollider == null)
            _boxCollider = GetComponent<BoxCollider>();

        if (_boxCollider == null)
            return;

        // Keep the editor visualization in sync with collider and offset edits.
        CacheOverlapBox();

        Gizmos.color = _isPlayerAbove || _isMonsterAbove ? Color.red : Color.green;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        DrawOverlapBoxGizmo(_overlapBoxCenter);
        Gizmos.matrix = previousMatrix;
    }

    private void DrawOverlapBoxGizmo(Vector3 center)
    {
        Gizmos.matrix = Matrix4x4.TRS(center, _overlapBoxRotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, _overlapBoxHalfExtents * 2f);
    }

    private void CheckObjectsAbove()
    {
        _isPlayerAbove = false;
        _isMonsterAbove = false;

        CheckObjectsInVolume(_overlapBoxCenter);
    }

    private void CheckObjectsInVolume(Vector3 center)
    {
        Collider[] overlappingObjects = Physics.OverlapBox(
            center,
            _overlapBoxHalfExtents,
            _overlapBoxRotation
        );
        foreach (Collider overlappingObject in overlappingObjects)
        {
            GameObject overlappingObjectRoot = overlappingObject.transform.root.gameObject;
            if (overlappingObjectRoot.layer == LayerMask.NameToLayer("Player"))
            {
                _isPlayerAbove = true;
                continue;
            }

            if (overlappingObjectRoot.CompareTag("Monster"))
            {
                _isMonsterAbove = true;
            }
        }
    }

    private void CheckRequirements()
    {
        bool requirementsMet = 
            (_requiresPlayer || _requiresMonster) && // At least one of the requirements must be turned on
            (!_requiresPlayer || _isPlayerAbove) &&
            (!_requiresMonster || _isMonsterAbove);

        if (_hasReportedRequirements && requirementsMet == _lastRequirementsMet)
            return;

        _hasReportedRequirements = true;
        _lastRequirementsMet = requirementsMet;
        OnPlateTriggeredInternal(requirementsMet);
        _onPlateTriggered?.Invoke(requirementsMet);
    }

    private void OnPlateTriggeredInternal(bool isTriggered)
    {
        transform.DOKill();
        if (isTriggered)
        {
            float pressDistance = _boxCollider.bounds.size.y - _pressedYHeight;
            transform.DOMoveY(_startYPosition - pressDistance, _animationDuration)
                .SetEase(_animationEase)
                .OnComplete(() => _onPlateFullyPressed?.Invoke());
        }
        else
        {
            transform.DOMoveY(_startYPosition, _animationDuration).SetEase(_animationEase);
        }
    }

    private void CacheReferences()
    {
        if (_boxCollider == null)
            _boxCollider = GetComponent<BoxCollider>();
    }

    private void CacheOverlapBox()
    {
        Vector3 scale = _boxCollider.transform.lossyScale;
        Vector3 colliderCenter = _boxCollider.transform.TransformPoint(_boxCollider.center);
        Vector3 offset = _checkYOffset * Vector3.up;
        _overlapBoxCenter = colliderCenter + offset * 0.5f;
        _overlapBoxHalfExtents = Vector3.Scale(_boxCollider.size * 0.5f, new Vector3(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z)));
        _overlapBoxRotation = _boxCollider.transform.rotation;

        // Enclose the original box and its vertically offset copy in one box.
        Vector3 localOffset = Quaternion.Inverse(_overlapBoxRotation) * offset;
        _overlapBoxHalfExtents += new Vector3(
            Mathf.Abs(localOffset.x),
            Mathf.Abs(localOffset.y),
            Mathf.Abs(localOffset.z)) * 0.5f;
    }
}
