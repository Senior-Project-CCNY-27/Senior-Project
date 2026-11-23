using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// Fires a SoundDataScriptableEvent when this feedback is played by an MMF Player.
/// </summary>
[AddComponentMenu("")]
[System.Serializable]
[FeedbackPath("Custom/Sound Data Event")]
[FeedbackHelp("Fires the assigned SoundDataScriptableEvent with the configured SoundData payload.")]
public class MMF_SoundDataEvent : MMF_Feedback
{
    [MMFInspectorGroup("Sound Data", true, 14)]
    [SerializeField] private SoundDataScriptableEvent _soundDataEvent;
    [SerializeField] private float _radius = 10f;
    [SerializeField] private float _volume = 100f;
    [SerializeField] private GameObject _source;
    [SerializeField] private bool _isSpatial = true;

    public override float FeedbackDuration => 0f;

    protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1f)
    {
        if (_soundDataEvent == null || _source == null)
            return;

        SoundData soundData = new SoundData
        {
            Radius = _radius,
            Volume = _volume,
            Source = _source,
            IsSpatial = _isSpatial,
            Position = _source.transform.position
        };

        _soundDataEvent.FireEvent(soundData);
    }
}
