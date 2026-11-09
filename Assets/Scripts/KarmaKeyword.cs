using System;
using UnityEngine;

public enum KarmaKeyword { Poison, Curse, Light, Dark }

[Serializable]
public class KarmaBead
{
    public const float DecayDuration = 3f;
    public KarmaKeyword keyword;
    public int power;
    [NonSerialized] private double decayDeadline;
    public float remaining
    {
        get => Mathf.Max(0f, (float)(decayDeadline - Time.realtimeSinceStartupAsDouble));
        set => decayDeadline = Time.realtimeSinceStartupAsDouble + Mathf.Max(0f, value);
    }
    public float lifetime = DecayDuration;
    public float Freshness => Mathf.Clamp01(remaining / Mathf.Max(0.01f, lifetime));
    public bool IsRotten => power > 0 && remaining <= 0f;
    public bool IsEmpty => power <= 0;
    public void BeginDecay()
    {
        lifetime = DecayDuration;
        decayDeadline = Time.realtimeSinceStartupAsDouble + DecayDuration;
    }
    public void Clear() { power = 0; decayDeadline = 0; }
}
