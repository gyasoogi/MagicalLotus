using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField, Min(1f)] private float startingHealth = 100f;
    private float health;
    public float Health => health;
    public float MaxHealth => maxHealth;
    public bool IsDead => health <= 0f;
    private void Awake()
    {
        health = Mathf.Clamp(startingHealth, 1f, maxHealth);
        if (GetComponent<GameRestartController>() == null) gameObject.AddComponent<GameRestartController>();
    }
    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f) return;
        health = Mathf.Max(0f, health - damage);
        if (!IsDead) return;
        GetComponent<KnotAttack>()?.CancelPath();
        GetComponent<PlayerMovement>()?.SetMovementLocked(true);
        StringDrawer drawer = GetComponent<StringDrawer>();
        if (drawer != null) drawer.enabled = false;
    }
    public void HealMissingFraction(float fraction)
    {
        if (!IsDead) health = Mathf.Min(maxHealth, health + (maxHealth - health) * Mathf.Clamp01(fraction));
    }
}
