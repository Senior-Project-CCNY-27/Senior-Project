using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public class DestructibleFragmentsHolder : MonoBehaviour
{
    [SerializeField, ReadOnly] private List<Rigidbody> _fragmentRigidBodies = new();
    
    [Header("Config")]
    [SerializeField] private float _explosionForce = 3f;
    [SerializeField] private float _lifeTime = 2f;
    
    [Header("Fade Away")]
    [SerializeField] private float _fadeAwayDuration = 0.5f;
    [SerializeField] private Ease _fadeAwayEase = Ease.OutCirc;

    private void OnValidate()
    {
        _fragmentRigidBodies = GetComponentsInChildren<Rigidbody>().ToList();
    }

    public void TriggerDestruction(Vector3 hitPoint)
    {
        transform.SetParent(null);
        gameObject.SetActive(true);
        
        foreach (Rigidbody rigidBody in _fragmentRigidBodies)
        {
            Vector3 direction = rigidBody.transform.position - hitPoint;
            rigidBody.AddForceAtPosition(direction.normalized * _explosionForce, transform.position, ForceMode.Impulse);
        }

        DOVirtual.DelayedCall(_lifeTime, FadeAway);
    }

    private void FadeAway()
    {
        Sequence sequence = DOTween.Sequence();
        foreach (Rigidbody rigidBody in _fragmentRigidBodies)
            sequence.Join(rigidBody.transform.DOScale(0f, _fadeAwayDuration).SetEase(_fadeAwayEase));

        sequence.OnComplete(() =>
        {
            Destroy(gameObject);
        });
    }
}