using System.Collections.Generic;
using UnityEngine;

// Drawing, extraction and hit stop share ownership so ending one cannot restore stale time.
public static class GameTimeEffects
{
    private static readonly Dictionary<object, float> requests = new Dictionary<object, float>();
    private static float baselineScale, baselineFixedStep;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { requests.Clear(); }

    public static void ResetForRestart()
    {
        if (requests.Count > 0) Time.fixedDeltaTime = baselineFixedStep;
        requests.Clear();
        Time.timeScale = baselineScale = 1f;
        baselineFixedStep = Time.fixedDeltaTime;
    }

    public static void Request(object owner, float scale)
    {
        if (requests.Count == 0)
        {
            baselineScale = Time.timeScale;
            baselineFixedStep = Time.fixedDeltaTime;
        }
        requests[owner] = Mathf.Clamp01(scale);
        Apply();
    }

    public static void Release(object owner)
    {
        if (!requests.Remove(owner)) return;
        if (requests.Count == 0)
        {
            Time.timeScale = baselineScale;
            Time.fixedDeltaTime = baselineFixedStep;
        }
        else Apply();
    }

    private static void Apply()
    {
        float scale = baselineScale;
        foreach (float requested in requests.Values) scale = Mathf.Min(scale, requested);
        Time.timeScale = scale;
        if (scale > 0f)
            Time.fixedDeltaTime = Mathf.Max(0.0001f, baselineFixedStep * scale / Mathf.Max(0.01f, baselineScale));
    }
}
