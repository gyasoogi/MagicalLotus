using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Enemy : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;

    private int health;
    private EnemyHitEffect hitEffect;

    private void Awake()
    {
        health = maxHealth;
        hitEffect = GetComponent<EnemyHitEffect>();

        // Enemies are attack targets, so they should not physically block the player.
        GetComponent<Collider2D>().isTrigger = true;
    }

    public void TakeDamage(int damage)
    {
        health -= damage;
        hitEffect?.Play();

        if (health <= 0)
            Destroy(gameObject);
    }
}
