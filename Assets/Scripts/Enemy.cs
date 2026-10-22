using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Enemy : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;

    [Header("Keyword tuning")]
    [SerializeField, Range(0f, 1f)] private float poisonFraction = 0.05f;
    [SerializeField, Min(0f)] private float curseDamagePerStack = 0.01f;
    [SerializeField, Range(0f, 1f)] private float lightHealFraction = 0.05f;
    [SerializeField, Range(0f, 1f)] private float darkDamageReduction = 0.1f;
    [SerializeField, Min(0f)] private float darkBurstFraction = 0.2f;
    private float health;
    private EnemyHitEffect hitEffect;
    private readonly List<Poison> poisons = new List<Poison>();
    private int curseStacks, darkWeaknessStacks, polarityPower;
    private KarmaKeyword? polarity;
    private float polarityRemaining, polarityBaseDamage;
    private PlayerHealth lightOwner;
    public bool IsDead => health <= 0f;
    public float Health => health;
    public float MaxHealth => maxHealth;
    public int CurseStacks => curseStacks;
    public string StatusText => (poisons.Count > 0 ? "독 " : "")
        + (curseStacks > 0 ? "저주 " : "")
        + (polarity.HasValue ? (polarity == KarmaKeyword.Light ? "빛" : "어둠") : "");
    public float OutgoingDamageMultiplier => Mathf.Max(0f, 1f - darkWeaknessStacks * darkDamageReduction);
    private class Poison { public float remaining = 5f; public float damagePerSecond; }

    private void Awake()
    {
        health = maxHealth;
        hitEffect = GetComponent<EnemyHitEffect>();

        // Enemies are attack targets, so they should not physically block the player.
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Start() { EnemyRespawner.Register(this); }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f) return;
        DamageDirect(damage * (1f + curseStacks * curseDamagePerStack));
    }

    private void DamageDirect(float damage)
    {
        if (IsDead || damage <= 0f) return;
        health = Mathf.Max(0f, health - damage);
        hitEffect?.Play();

        if (health <= 0)
        {
            if (polarity.HasValue) RemovePolarity();
            Destroy(gameObject);
        }
    }

    public void ApplyKeyword(KarmaKeyword keyword, int power, float hitDamage, PlayerHealth owner)
    {
        if (IsDead || power <= 0) return;
        power = Mathf.Clamp(power, 1, 8);
        switch (keyword)
        {
            case KarmaKeyword.Poison:
                // Fixed damage bypasses curse amplification and totals 5% per captured bullet.
                poisons.Add(new Poison { damagePerSecond = hitDamage * poisonFraction * power / 5f });
                break;
            case KarmaKeyword.Curse:
                curseStacks = Mathf.Min(30, curseStacks + power);
                break;
            default:
                if (polarity.HasValue && polarity.Value != keyword) RemovePolarity();
                if (IsDead) return;
                polarity = keyword;
                polarityPower = power;
                polarityRemaining = 6f;
                polarityBaseDamage = hitDamage;
                lightOwner = owner;
                break;
        }
    }

    private void Update()
    {
        if (IsDead) return;
        for (int i = poisons.Count - 1; i >= 0; i--)
        {
            Poison poison = poisons[i];
            float elapsed = Mathf.Min(Time.deltaTime, poison.remaining);
            poison.remaining -= elapsed;
            DamageDirect(poison.damagePerSecond * elapsed);
            if (poison.remaining <= 0f) poisons.RemoveAt(i);
        }
        if (!polarity.HasValue) return;
        polarityRemaining -= Time.deltaTime;
        if (polarityRemaining <= 0f) RemovePolarity();
    }

    private void RemovePolarity()
    {
        KarmaKeyword? previous = polarity;
        polarity = null;
        if (previous == KarmaKeyword.Light)
            lightOwner?.HealMissingFraction(lightHealFraction * polarityPower);
        else if (previous == KarmaKeyword.Dark)
        {
            darkWeaknessStacks = Mathf.Min(3, darkWeaknessStacks + 1);
            TakeDamage(polarityBaseDamage * darkBurstFraction * polarityPower);
        }
    }

    public void ClearCurse() { curseStacks = 0; }
}
