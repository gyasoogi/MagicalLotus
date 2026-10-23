using UnityEngine;

// Lives independently of the chasing orb and player, including after a fatal hit.
public class KarmaExplosionEffect : MonoBehaviour
{
    private const float Duration = 0.95f;
    private readonly LineRenderer[] waves = new LineRenderer[3];
    private LineRenderer boundary;
    private SpriteRenderer flash;
    private Sprite flashSprite;
    private Texture2D glowTexture;
    private Material ringMaterial, particleMaterial;
    private float startedAt;
    public float Radius { get; private set; }

    public static KarmaExplosionEffect Spawn(Vector3 position, float radius)
    {
        GameObject obj = new GameObject("Karma Explosion Effect");
        obj.transform.position = position;
        KarmaExplosionEffect effect = obj.AddComponent<KarmaExplosionEffect>();
        effect.Radius = Mathf.Max(0.1f, radius);
        effect.startedAt = Time.unscaledTime;
        effect.ringMaterial = new Material(Shader.Find("Sprites/Default"));
        effect.glowTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        effect.glowTexture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                effect.glowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - distance), 1.6f)));
            }
        effect.glowTexture.Apply();
        effect.particleMaterial = new Material(effect.ringMaterial) { mainTexture = effect.glowTexture };
        for (int i = 0; i < effect.waves.Length; i++) effect.waves[i] = effect.CreateRing("Shockwave " + i, 0.14f);
        effect.boundary = effect.CreateRing("Damage Boundary", 0.055f);
        effect.boundary.transform.localScale = Vector3.one * effect.Radius;
        GameObject core = new GameObject("Blast Flash");
        core.transform.SetParent(obj.transform, false);
        effect.flash = core.AddComponent<SpriteRenderer>();
        effect.flashSprite = Sprite.Create(effect.glowTexture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 64f);
        effect.flash.sprite = effect.flashSprite;
        effect.flash.sortingOrder = 39;
        effect.EmitParticles("Embers", 96, false);
        effect.EmitParticles("Purple Smoke", 40, true);
        effect.Update();
        return effect;
    }

    private LineRenderer CreateRing(string title, float width)
    {
        GameObject obj = new GameObject(title);
        obj.transform.SetParent(transform, false);
        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.sharedMaterial = ringMaterial;
        line.useWorldSpace = false;
        line.loop = true;
        line.widthMultiplier = width;
        line.sortingOrder = 43;
        line.positionCount = 96;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), -0.1f));
        }
        return line;
    }

    private void EmitParticles(string title, int count, bool smoke)
    {
        GameObject obj = new GameObject(title);
        obj.transform.SetParent(transform, false);
        ParticleSystem particles = obj.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = Duration;
        main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count;
        var emission = particles.emission; emission.enabled = false;
        var shape = particles.shape; shape.enabled = false;
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.25f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, smoke ? 2f : 0.15f));
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.sortingOrder = smoke ? 40 : 42;
        particles.Play();
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Color color = smoke ? new Color(0.45f, 0.08f, 0.65f, 0.45f)
                : Color.Lerp(new Color(1f, 0.25f, 0.05f), new Color(1f, 0.85f, 0.35f), Random.value);
            var particle = new ParticleSystem.EmitParams {
                position = transform.position + (Vector3)(Random.insideUnitCircle * Radius * 0.12f),
                velocity = (Vector3)(direction * Radius * Random.Range(smoke ? 0.2f : 1.2f, smoke ? 0.8f : 2.6f)),
                startLifetime = Random.Range(smoke ? 0.65f : 0.3f, smoke ? 0.9f : 0.7f),
                startSize = Random.Range(smoke ? 0.4f : 0.08f, smoke ? 0.9f : 0.24f),
                startColor = color
            };
            particles.Emit(particle, 1);
        }
    }

    private void Update()
    {
        float elapsed = Time.unscaledTime - startedAt;
        if (elapsed >= Duration) { Destroy(gameObject); return; }
        for (int i = 0; i < waves.Length; i++)
        {
            float phase = (elapsed - i * 0.09f) / 0.55f;
            waves[i].enabled = phase >= 0f && phase < 1f;
            float progress = Mathf.Clamp01(phase);
            waves[i].transform.localScale = Vector3.one * Radius * Mathf.Lerp(0.12f, 1f, 1f - Mathf.Pow(1f - progress, 3f));
            waves[i].startColor = waves[i].endColor = Color.Lerp(new Color(1f, 0.75f, 0.4f, 1f - progress),
                new Color(0.8f, 0.15f, 1f, 0f), progress);
        }
        boundary.startColor = boundary.endColor = new Color(1f, 0.3f, 0.1f, 0.6f * Mathf.Clamp01(1f - elapsed / 0.65f));
        flash.transform.localScale = Vector3.one * Radius * 2f * Mathf.Lerp(0.2f, 0.85f, Mathf.Clamp01(elapsed / 0.25f));
        flash.color = new Color(1f, 0.55f, 0.2f, 0.9f * Mathf.Clamp01(1f - elapsed / 0.4f));
    }

    private void OnDestroy()
    {
        if (ringMaterial != null) Destroy(ringMaterial);
        if (particleMaterial != null) Destroy(particleMaterial);
        if (flashSprite != null) Destroy(flashSprite);
        if (glowTexture != null) Destroy(glowTexture);
    }
}
