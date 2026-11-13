using UnityEngine;
using UnityEngine.UI;

public class LowHealthUI : MonoBehaviour
{
    [SerializeField] Health health;
    [SerializeField] Image bloodOverlay;
    [SerializeField] int lowHealthThreshold = 1;
    [SerializeField] float fadeSpeed = 5f;
    [SerializeField] float pulseSpeed = 3f;
    [SerializeField] float minAlpha = 0.5f;
    [SerializeField] float maxAlpha = 0.9f;

    int currentHP;

    void OnEnable()  => health.OnHealthChanged.AddListener(HandleHealthChanged);
    void OnDisable() => health.OnHealthChanged.RemoveListener(HandleHealthChanged);

    void Start()
    {
        currentHP = health.CurrentHealth;
    }

    void HandleHealthChanged(int prev, int curr)
    {
        currentHP = curr;
    }

    void Update()
    {
        Color c = bloodOverlay.color;
        float target = 0f;

        if (currentHP > 0 && currentHP <= lowHealthThreshold)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
            target = Mathf.Lerp(minAlpha, maxAlpha, t);
        }

        c.a = Mathf.Lerp(c.a, target, Time.deltaTime * fadeSpeed);
        bloodOverlay.color = c;
    }
}