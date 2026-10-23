using UnityEngine;

// No CapturableProjectile component: strings cannot absorb this orb.
public class KarmaOutbreak : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float explosionRadius = 3.5f;
    private PlayerHealth target;
    private Rigidbody2D targetBody;
    private Vector2 TargetPosition => targetBody != null ? targetBody.position : (Vector2)target.transform.position;
    private float speed, damage, remaining;
    private LineRenderer ring, dangerRing;
    private float launchAt, flightRemaining;
    public bool IsTelegraphing => !HasExploded && Time.unscaledTime < launchAt;
    public bool HasExploded { get; private set; }
    public float ExplosionRadius => explosionRadius;
    public static KarmaOutbreak Spawn(PlayerHealth target, float speed, float damage, float lifetime)
    {
        GameObject obj = new GameObject("Karma Outbreak");
        Rigidbody2D body = target.GetComponent<Rigidbody2D>();
        obj.transform.position = (body != null ? (Vector3)body.position : target.transform.position) + Vector3.up * 2f;
        KarmaOutbreak orb = obj.AddComponent<KarmaOutbreak>();
        orb.target = target;
        orb.targetBody = body;
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
        GameObject warning = new GameObject("Outbreak Blast Radius");
        warning.transform.SetParent(obj.transform, false);
        orb.dangerRing = warning.AddComponent<LineRenderer>();
        orb.dangerRing.sharedMaterial = orb.ring.sharedMaterial;
        orb.dangerRing.useWorldSpace = false;
        orb.dangerRing.loop = true;
        orb.dangerRing.widthMultiplier = 0.035f;
        orb.dangerRing.sortingOrder = 29;
        orb.dangerRing.positionCount = 72;
        for (int i = 0; i < 72; i++)
        {
            float angle = i * Mathf.PI * 2f / 72f;
            orb.dangerRing.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * orb.ExplosionRadius);
        }
        orb.dangerRing.startColor = orb.dangerRing.endColor = new Color(1f, 0.15f, 0.35f, 0.25f);
        return orb;
    }
    private void Update()
    {
        if (HasExploded) return;
        if (target == null || target.IsDead) { Destroy(gameObject); return; }
        if (IsTelegraphing)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 20f) * 0.25f;
            ring.widthMultiplier = 0.2f * pulse;
            ring.startColor = ring.endColor = Color.Lerp(Color.red, Color.white, pulse - 0.75f);
            dangerRing.startColor = dangerRing.endColor = new Color(1f, 0.15f, 0.35f, 0.2f + (pulse - 0.75f) * 0.5f);
            return;
        }
        ring.widthMultiplier = 0.25f;
        ring.startColor = ring.endColor = new Color(0.75f, 0.1f, 0.8f);
        dangerRing.startColor = dangerRing.endColor = new Color(1f, 0.15f, 0.35f, 0.2f);
        remaining -= Time.deltaTime;
        flightRemaining -= Time.deltaTime;
        Vector3 destination = TargetPosition;
        destination.z = transform.position.z;
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
        if (flightRemaining <= 0f || remaining <= 0f || Vector2.Distance(transform.position, destination) <= 0.45f) Explode();
    }
    private void Explode()
    {
        if (HasExploded) return;
        HasExploded = true;
        KarmaExplosionEffect.Spawn(transform.position, ExplosionRadius);
        CameraShake.Instance?.Play(0.35f, 0.25f);
        if (Vector2.Distance(transform.position, TargetPosition) <= ExplosionRadius) target.TakeDamage(damage);
        Destroy(gameObject);
    }
    private void OnDestroy()
    {
        if (ring != null && ring.sharedMaterial != null) Destroy(ring.sharedMaterial);
    }
}
