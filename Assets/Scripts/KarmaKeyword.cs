using System;
using UnityEngine;

public enum KarmaKeyword { Poison, Curse, Light, Dark }

[Serializable]
public class KarmaBead
{
    public KarmaKeyword keyword;
    public int power;
    public float remaining;
    public float lifetime = 20f;
    public float Freshness => Mathf.Clamp01(remaining / Mathf.Max(0.01f, lifetime));
    public bool IsRotten => power > 0 && remaining <= 0f;
    public bool IsEmpty => power <= 0;
    public void Clear() { power = 0; remaining = 0f; }
}
