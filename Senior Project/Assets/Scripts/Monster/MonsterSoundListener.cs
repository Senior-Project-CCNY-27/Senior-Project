using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class MonsterSoundListener : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Required] private SoundDataScriptableEvent _soundDataEventListener;
    [SerializeField, Required] private Transform _sourceListenerTransform;
    
    [Header("Config")]
    [SerializeField] private float _wallDampeningMultiplier = 0.75f; 
    [SerializeField] private AnimationCurve _distanceDampeningCurve;
    [SerializeField] private bool _isDebugEnabled = true;
    
    public event Action<SoundData> OnValidSoundHeard = delegate { };

    private void OnEnable()
    {
        _soundDataEventListener.OnEventFired += OnSoundDataEventReceived;
    }

    private void OnDisable()
    {
        _soundDataEventListener.OnEventFired -= OnSoundDataEventReceived;
    }
    
    private void OnSoundDataEventReceived(SoundData soundData)
    {
        if (_isDebugEnabled)
            CustomDebug.InstantiateTemporarySphere(soundData.Position, soundData.Radius, 2f, Color.blue.WithA(0.2f));
        
        float soundDistance = Vector3.Distance(soundData.Position, _sourceListenerTransform.position);
        if (soundDistance > soundData.Radius)
            return;
        
        if (soundData.IsSpatial)
        {
            Vector3 dirToListener = _sourceListenerTransform.position - soundData.Position;
            RaycastHit[] wallsInBetween = Physics.RaycastAll(soundData.Position, dirToListener.normalized, soundDistance,
                LayerMask.GetMask("Wall"));

            float distanceAdjustedVolume = _distanceDampeningCurve.Evaluate(soundDistance / soundData.Radius) * soundData.Volume;
            float finalVolume = Mathf.Pow(_wallDampeningMultiplier, wallsInBetween.Length) * distanceAdjustedVolume;

            soundData.Volume = finalVolume;
        }
        
        if (_isDebugEnabled)
            Debug.Log($"{name} processed sound with data: [{soundData}]");
        
        OnValidSoundHeard?.Invoke(soundData);
    }
}