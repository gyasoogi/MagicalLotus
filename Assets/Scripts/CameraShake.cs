using UnityEngine;

[DefaultExecutionOrder(10000)]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private float endTime;
    private float duration;
    private float strength;

    private void Awake()
    {
        Instance = this;
    }

    public void Play(float newDuration, float newStrength)
    {
        duration = newDuration;
        strength = newStrength;
        endTime = Time.unscaledTime + duration;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= endTime) return;

        float amount = (endTime - Time.unscaledTime) / duration;
        Vector2 offset = Random.insideUnitCircle * strength * amount;
        transform.position += new Vector3(offset.x, offset.y, 0f);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
