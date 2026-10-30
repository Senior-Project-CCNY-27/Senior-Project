using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 2;
    [SerializeField] private float invincibilityTime = 1.5f;
    [SerializeField] private TMP_Text healthText;

    private int health;
    private float nextHitTime;

    private void Start()
    {
        health = maxHealth;
        UpdateText();
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.gameObject.CompareTag("Monster")) TakeDamage();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Monster")) TakeDamage();
    }

    public void TakeDamage()
    {
        if (Time.time < nextHitTime || health <= 0) return;

        nextHitTime = Time.time + invincibilityTime;
        health--;
        UpdateText();

        if (health <= 0)
            GameManager.Instance.ChangeState(GameManager.State.GameOver);
    }

    private void UpdateText() => healthText.text = $"Health: {health}";
}