using System.Collections.Generic;
using UnityEngine;

// Keeps an inactive copy of the scene enemy, including its projectile/target references.
public class EnemyRespawner : MonoBehaviour
{
    [SerializeField, Min(0f)] private float respawnDelay = 0.75f;
    [SerializeField, Range(1, 8)] private int simultaneousEnemies = 2;
    [SerializeField, Min(1f)] private float enemySpacing = 3f;
    private Enemy template;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private PlayerHealth player;
    private class Slot { public Enemy enemy; public Vector3 position; public float remaining; }
    private readonly List<Slot> slots = new List<Slot>();
    public Enemy CurrentEnemy => slots.Count > 0 ? slots[0].enemy : null;
    public IEnumerable<Enemy> AliveEnemies
    {
        get { foreach (Slot slot in slots) if (slot.enemy != null && !slot.enemy.IsDead) yield return slot.enemy; }
    }
    public int AliveCount { get { int count = 0; foreach (var enemy in AliveEnemies) count++; return count; } }
    public int SpawnCount { get; private set; }

    public static void Register(Enemy enemy)
    {
        EnemyRespawner existing = FindFirstObjectByType<EnemyRespawner>();
        if (existing != null) return;
        GameObject obj = new GameObject("Enemy Respawner");
        EnemyRespawner spawner = obj.AddComponent<EnemyRespawner>();
        spawner.Initialize(enemy);
    }

    public void Initialize(Enemy enemy)
    {
        if (template != null || enemy == null) return;
        spawnPosition = enemy.transform.position;
        spawnRotation = enemy.transform.rotation;
        player = FindFirstObjectByType<PlayerHealth>();
        GameObject holder = new GameObject("Inactive Enemy Template");
        holder.transform.SetParent(transform);
        holder.SetActive(false);
        template = Instantiate(enemy, holder.transform);
        template.name = "Enemy Template";
        template.transform.localScale = enemy.transform.lossyScale;
        slots.Add(new Slot { enemy = enemy, position = spawnPosition, remaining = respawnDelay });
        SpawnCount = 1;
        for (int i = 1; i < Mathf.Clamp(simultaneousEnemies, 1, 8); i++)
        {
            float offset = ((i + 1) / 2) * enemySpacing * (i % 2 == 1 ? -1f : 1f);
            Slot slot = new Slot { position = spawnPosition + Vector3.right * offset, remaining = respawnDelay };
            slots.Add(slot);
            Spawn(slot, i);
        }
    }

    private void Update()
    {
        if (template == null || (player != null && player.IsDead)) return;
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot.enemy != null) continue;
            slot.remaining -= Time.deltaTime;
            if (slot.remaining <= 0f) Spawn(slot, i);
        }
    }
    private void Spawn(Slot slot, int index)
    {
        slot.enemy = Instantiate(template, slot.position, spawnRotation);
        slot.enemy.name = "Enemy " + ++SpawnCount;
        slot.enemy.GetComponent<EnemyProjectileThrower>()?.SetInitialDelay(0.65f + index * 0.65f);
        slot.enemy.gameObject.SetActive(true);
        slot.remaining = Mathf.Max(0f, respawnDelay);
    }
}
