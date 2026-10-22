using UnityEngine;

public class KarmaMovementEffect : MonoBehaviour
{
    private TrailRenderer trail;
    private ParticleSystem particles;
    private Material material;
    private bool active;
    public bool IsEmitting => trail != null && trail.emitting;
    public KarmaKeyword? Keyword { get; private set; }

    private void Awake()
    {
        GameObject visual = new GameObject("Karma Movement Effect");
        visual.transform.SetParent(transform, false);
        material = new Material(Shader.Find("Sprites/Default"));
        trail = visual.AddComponent<TrailRenderer>();
        trail.material = material;
        trail.time = 0.3f;
        trail.minVertexDistance = 0.05f;
        trail.sortingOrder = 25;
        trail.emitting = false;
        particles = visual.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = 0.25f;
        main.startSpeed = 0.6f;
        main.startSize = 0.12f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = particles.emission; emission.rateOverTime = 24;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.12f;
        particles.GetComponent<ParticleSystemRenderer>().material = material;
        particles.GetComponent<ParticleSystemRenderer>().sortingOrder = 26;
    }

    public void Begin(KarmaKeyword? keyword, int power)
    {
        Keyword = keyword;
        active = keyword.HasValue;
        trail.Clear();
        Color color = keyword.HasValue ? KarmaBeadHud.KeywordColor(keyword.Value) : Color.white;
        trail.startColor = color;
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        trail.startWidth = 0.2f + Mathf.Min(8, power) * 0.025f;
        trail.endWidth = 0.02f;
        var main = particles.main;
        main.startColor = color;
        main.gravityModifier = keyword == KarmaKeyword.Light ? -0.15f : keyword == KarmaKeyword.Poison ? 0.15f : 0f;
        main.startSpeed = keyword == KarmaKeyword.Curse ? 1.2f : 0.6f;
        SetMoving(false);
    }

    public void SetMoving(bool moving)
    {
        bool emit = active && moving;
        trail.emitting = emit;
        if (emit && !particles.isPlaying) particles.Play();
        else if (!emit && particles.isPlaying) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public void Stop() { active = false; SetMoving(false); }
    private void OnDisable() { if (trail != null) Stop(); }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
