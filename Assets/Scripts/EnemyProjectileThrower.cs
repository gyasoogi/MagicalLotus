using UnityEngine;

public class EnemyProjectileThrower : MonoBehaviour
{
    [SerializeField] private CapturableProjectile projectilePrefab;
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 throwOffset = new Vector2(0f, 0.6f);
    [SerializeField] private float throwInterval = 2.5f;
    [SerializeField] private float firstThrowDelay = 1f;
    [SerializeField] private float flightTime = 1.2f;
    [SerializeField, Min(1)] private int projectilesPerVolley = 4;
    [SerializeField, Min(1f)] private float intervalMultiplier = 1.2f;
    [SerializeField, Range(0f, 30f)] private float volleySpreadDegrees = 8f;

    private float throwTimer;
    [SerializeField] private bool cycleKeywords = true;
    private int nextKeyword;
    [SerializeField, Min(0)] private int unabsorbableEveryVolleys = 3;
    private int volleyCount;
    public int ProjectilesPerVolley => Mathf.Max(1, projectilesPerVolley);
    public float ThrowInterval => Mathf.Max(0.1f, throwInterval * intervalMultiplier);
    public void SetInitialDelay(float delay) { firstThrowDelay = Mathf.Max(0f, delay); throwTimer = firstThrowDelay; }

    private void Start()
    {
        throwTimer = firstThrowDelay;
    }

    private void Update()
    {
        if (projectilePrefab == null || target == null) return;

        throwTimer -= Time.deltaTime;
        if (throwTimer > 0f) return;

        ThrowProjectile();
        throwTimer = ThrowInterval;
    }

    private void ThrowProjectile()
    {
        Vector2 start = (Vector2)transform.position + throwOffset;
        Enemy enemy = GetComponentInParent<Enemy>();
        float safeFlightTime = Mathf.Max(0.1f, flightTime);
        Vector2 distance = (Vector2)target.position - start;
        int count = ProjectilesPerVolley;
        volleyCount++;
        for (int i = 0; i < count; i++)
        {
            CapturableProjectile projectile = Instantiate(projectilePrefab, start, Quaternion.identity);
            if (cycleKeywords) projectile.SetKeyword((KarmaKeyword)nextKeyword);
            if (unabsorbableEveryVolleys > 0 && volleyCount % unabsorbableEveryVolleys == 0 && i == count / 2)
                projectile.SetAbsorbable(false);
            if (enemy != null) projectile.SetSourceDamageMultiplier(enemy.OutgoingDamageMultiplier);
            float gravity = Mathf.Abs(Physics2D.gravity.y * projectile.GravityScale);
            Vector2 velocity = new Vector2(distance.x / safeFlightTime,
                distance.y / safeFlightTime + gravity * safeFlightTime * 0.5f);
            float angle = (i - (count - 1) * 0.5f) * volleySpreadDegrees;
            projectile.Launch(Quaternion.Euler(0f, 0f, angle) * velocity);
        }
        if (cycleKeywords) nextKeyword = (nextKeyword + 1) % 4;
    }
}
