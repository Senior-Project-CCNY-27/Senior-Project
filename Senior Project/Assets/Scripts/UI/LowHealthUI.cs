using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class LowHealthUI : MonoBehaviour
{
    [SerializeField, Required] Health _health;
    [SerializeField, Required] Image _bloodOverlay;
    [SerializeField] int _lowHealthThreshold = 1;
    [SerializeField] float _fadeSpeed = 5f;
    [SerializeField] float _pulseSpeed = 3f;
    [SerializeField] private FloatRange _alphaRange = new FloatRange(0.5f, 0.9f); 

    int currentHP;

    void OnEnable()  => _health.OnHealthChanged.AddListener(HandleHealthChanged);
    void OnDisable() => _health.OnHealthChanged.RemoveListener(HandleHealthChanged);

    void Start()
    {
        currentHP = _health.CurrentHealth;
    }

    void HandleHealthChanged(int prev, int curr)
    {
        currentHP = curr;
    }

    void Update()
    {
        Color c = _bloodOverlay.color;
        float target = 0f;

        if (currentHP > 0 && currentHP <= _lowHealthThreshold)
        {
            float t = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) / 2f;
            target = _alphaRange.Lerp(t);
        }

        c.a = Mathf.Lerp(c.a, target, Time.deltaTime * _fadeSpeed);
        _bloodOverlay.color = c;
    }
}