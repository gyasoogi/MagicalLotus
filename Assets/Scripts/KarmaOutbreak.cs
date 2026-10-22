using UnityEngine;

// No CapturableProjectile component: strings cannot absorb this orb.
public class KarmaOutbreak : MonoBehaviour
{
    private PlayerHealth target;
    private float speed, damage, remaining;
    private LineRenderer ring;
    private float launchAt, explodeUntil, flightRemaining;
    public bool IsTelegraphing => !HasExploded && Time.unscaledTime < launchAt;
    public bool HasExploded { get; private set; }
    public float ExplosionRadius => 1.25f;
    public static KarmaOutbreak Spawn(PlayerHealth target, float speed, float damage, float lifetime)
    {
        GameObject obj = new GameObject("Karma Outbreak");
        obj.transform.position = target.transform.position + Vector3.up * 2f;
        KarmaOutbreak orb = obj.AddComponent<KarmaOutbreak>();
        orb.target = target;
        orb.speed = Mathf.Max(0.1f, speed);
        orb.damage = damage;
        orb.remaining = Mathf.Max(1f, lifetime);
        orb.flightRemaining = Mathf.Min(3f, orb.remaining);
        orb.launchAt = Time.unscaledTime + 0.6f;
        orb.ring = obj.AddComponent<LineRenderer>();
        orb.ring.material = new Material(Shader.Find("Sprites/Default"));
        orb.ring.useWorldSpace = false;
        orb.ring.loop = true;
        orb.ring.widthMultiplier = 0.25f;
        orb.ring.startColor = orb.ring.endColor = new Color(0.5f, 0.05f, 0.8f);
        orb.ring.sortingOrder = 30;
        orb.ring.positionCount = 24;
        for (int i = 0; i < 24; i++)
        {
            float angle = i * Mathf.PI * 2f / 24f;
            orb.ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.2f);
        }
        return orb;
    }
    private void Update()
    {
        if (target == null || target.IsDead) { Destroy(gameObject); return; }
        if (HasExploded)
        {
            if (Time.unscaledTime >= explodeUntil) Destroy(gameObject);
            return;
        }
        if (IsTelegraphing)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 20f) * 0.25f;
            transform.localScale = Vector3.one * pulse;
            ring.startColor = ring.endColor = Color.Lerp(Color.red, Color.white, pulse - 0.75f);
            return;
        }
        transform.localScale = Vector3.one;
        ring.startColor = ring.endColor = new Color(0.75f, 0.1f, 0.8f);
        remaining -= Time.deltaTime;
        flightRemaining -= Time.deltaTime;
        Vector3 destination = target.transform.position;
        destination.z = transform.position.z;
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
        if (flightRemaining <= 0f || remaining <= 0f || Vector2.Distance(transform.position, destination) <= 0.45f) Explode();
    }
    private void Explode()
    {
        HasExploded = true;
        explodeUntil = Time.unscaledTime + 0.25f;
        transform.localScale = Vector3.one * (ExplosionRadius / 0.2f);
        ring.widthMultiplier = 0.05f;
        ring.startColor = ring.endColor = new Color(1f, 0.2f, 0.1f, 0.7f);
        if (Vector2.Distance(transform.position, target.transform.position) <= ExplosionRadius) target.TakeDamage(damage);
    }
    private void OnDestroy()
    {
        if (ring != null && ring.sharedMaterial != null) Destroy(ring.sharedMaterial);
    }
}
