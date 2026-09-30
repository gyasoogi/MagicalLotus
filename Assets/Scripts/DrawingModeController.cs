using UnityEngine;

public class DrawingModeController : MonoBehaviour
{
    [SerializeField] private float drawingTimeScale = 0.08f;
    [SerializeField] private DrawingScreenEffect screenEffect;

    private float previousTimeScale = 1f;
    private float previousFixedDeltaTime = 0.02f;
    private bool drawingModeActive;

    public void BeginDrawing()
    {
        if (drawingModeActive) return;

        drawingModeActive = true;
        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;

        float slowScale = Mathf.Clamp(drawingTimeScale, 0.01f, 1f);
        Time.timeScale = slowScale;
        Time.fixedDeltaTime = previousFixedDeltaTime * slowScale;
        screenEffect?.SetActive(true);
    }

    public void EndDrawing()
    {
        if (!drawingModeActive) return;

        drawingModeActive = false;
        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;
        screenEffect?.SetActive(false);
    }

    private void OnDisable()
    {
        EndDrawing();
    }
}
