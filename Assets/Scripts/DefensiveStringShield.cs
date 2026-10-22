using System.Collections.Generic;
using UnityEngine;

// An open defensive stroke remains in the world without holding global slow motion.
public class DefensiveStringShield : MonoBehaviour
{
    private ProjectileCapture capture;
    private PlayerHealth ownerHealth;
    private readonly List<Vector3> points = new List<Vector3>();
    private LineRenderer line;
    private Material material;
    private Color color;
    private float expiresAt;
    public float RemainingSeconds => Mathf.Max(0f, expiresAt - Time.unscaledTime);

    public static DefensiveStringShield Create(ProjectileCapture owner, List<Vector3> stroke, LineRenderer source, float duration)
    {
        GameObject obj = new GameObject("Defensive String Shield");
        DefensiveStringShield shield = obj.AddComponent<DefensiveStringShield>();
        shield.capture = owner;
        shield.ownerHealth = owner.GetComponent<PlayerHealth>();
        shield.points.AddRange(stroke);
        shield.expiresAt = Time.unscaledTime + Mathf.Max(0.1f, duration);
        shield.line = obj.AddComponent<LineRenderer>();
        shield.material = new Material(Shader.Find("Sprites/Default"));
        shield.material.color = Color.white;
        shield.line.sharedMaterial = shield.material;
        shield.line.widthCurve = source.widthCurve;
        shield.line.widthMultiplier = source.widthMultiplier;
        shield.line.sortingLayerID = source.sortingLayerID;
        shield.line.sortingOrder = source.sortingOrder;
        shield.line.numCapVertices = shield.line.numCornerVertices = 6;
        shield.line.useWorldSpace = true;
        shield.line.positionCount = shield.points.Count;
        shield.line.SetPositions(shield.points.ToArray());
        shield.color = source.startColor;
        shield.line.startColor = shield.line.endColor = shield.color;
        return shield;
    }

    private void Update()
    {
        if (capture == null || !capture.isActiveAndEnabled || ownerHealth == null || ownerHealth.IsDead || RemainingSeconds <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        capture.SlowAlongString(points, allowDistantSlow: true);
        Color fading = color;
        fading.a *= Mathf.Clamp01(RemainingSeconds);
        line.startColor = line.endColor = fading;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
