using System.Collections;
using UnityEngine;

public class EnemyHitEffect : MonoBehaviour
{
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashTime = 0.06f;
    [SerializeField] private float punchScale = 1.15f;

    private Renderer bodyRenderer;
    private Color normalColor;
    private Vector3 normalScale;

    private void Awake()
    {
        bodyRenderer = GetComponentInChildren<Renderer>();
        normalScale = transform.localScale;
        if (bodyRenderer != null) normalColor = bodyRenderer.material.color;
    }

    public void Play()
    {
        StopAllCoroutines();
        StartCoroutine(Flash());
        SpawnParticles();
    }

    private IEnumerator Flash()
    {
        transform.localScale = normalScale * punchScale;
        if (bodyRenderer != null) bodyRenderer.material.color = flashColor;

        yield return new WaitForSecondsRealtime(flashTime);

        transform.localScale = normalScale;
        if (bodyRenderer != null) bodyRenderer.material.color = normalColor;
    }

    private void SpawnParticles()
    {
        GameObject effect = new GameObject("Hit Particles");
        effect.transform.position = transform.position;

        ParticleSystem particles = effect.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = 0.2f;
        main.startSpeed = 4f;
        main.startSize = 0.12f;
        main.startColor = new Color(1f, 0.75f, 0.15f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;

        particles.Play();
        Destroy(effect, 0.5f);
    }
}
