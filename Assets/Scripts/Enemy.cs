using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] private int maxHP = 3;

    private int currentHP;

    private void Awake()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(int damage)
    {
        currentHP -= damage;

        Debug.Log("적 HP: " + currentHP);

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("적 처치!");
        Destroy(gameObject);
    }
}