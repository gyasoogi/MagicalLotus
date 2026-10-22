using UnityEngine;

public class DrawingModeController : MonoBehaviour
{
    [SerializeField] private float drawingTimeScale = 0.08f;
    [SerializeField, Range(0.01f, 1f)] private float defensiveTimeScale = 0.03f;
    [SerializeField] private DrawingScreenEffect screenEffect;

    private bool drawingModeActive;

    private void ResolveScreenEffect()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        if (screenEffect == null || screenEffect.gameObject != camera.gameObject)
            screenEffect = camera.GetComponent<DrawingScreenEffect>() ?? camera.gameObject.AddComponent<DrawingScreenEffect>();
        screenEffect.enabled = true;
    }

    public void BeginDrawing(bool defensive = false)
    {
        if (drawingModeActive) return;

        drawingModeActive = true;
        ResolveScreenEffect();
        float slowScale = Mathf.Clamp(defensive ? defensiveTimeScale : drawingTimeScale, 0.01f, 1f);
        GameTimeEffects.Request(this, slowScale);
        screenEffect?.SetActive(true);
    }

    public void EndDrawing()
    {
        if (!drawingModeActive) return;

        drawingModeActive = false;
        GameTimeEffects.Release(this);
        screenEffect?.SetActive(false);
    }

    private void OnDisable()
    {
        EndDrawing();
    }
}
