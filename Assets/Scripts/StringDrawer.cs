using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(DrawingModeController))]
public class StringDrawer : MonoBehaviour
{
    [SerializeField] public LineRenderer lineRenderer;
    [SerializeField] private float minPointDistance = 0.08f;
    [SerializeField] private float wallPadding = 0.05f;
    [SerializeField, Min(0.1f)] private float shieldDuration = 5f;
    [SerializeField, Range(1, 8)] private int maximumShields = 3;

    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<DefensiveStringShield> shields = new List<DefensiveStringShield>();
    private Camera mainCamera;
    private KnotAttack attack;
    private ProjectileCapture projectileCapture;
    private DrawingModeController drawingMode;
    private Collider2D playerCollider;
    private bool isDrawing;
    private bool drawingBlocked;
    private bool defensiveDrawing;
    public bool IsDrawing => isDrawing;
    public bool IsDefensiveDrawing => isDrawing && defensiveDrawing;

    private void Awake()
    {
        mainCamera = Camera.main;
        attack = GetComponent<KnotAttack>();
        projectileCapture = GetComponent<ProjectileCapture>();
        drawingMode = GetComponent<DrawingModeController>();
        playerCollider = GetComponent<Collider2D>();
        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 0;
        }
    }

    private void Update()
    {
        if (Mouse.current == null || lineRenderer == null || mainCamera == null) return;

        if (!isDrawing)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame) StartDrawing(false);
            else if (Mouse.current.rightButton.wasPressedThisFrame) StartDrawing(true);
        }
        if (!isDrawing) return;
        var button = defensiveDrawing ? Mouse.current.rightButton : Mouse.current.leftButton;
        if (button.isPressed) DrawString();
        if (button.wasReleasedThisFrame) EndDrawing();
    }

    private void StartDrawing(bool defensive)
    {
        if (KarmaBeadHud.IsPointerOverHud() || (GetComponent<KarmaBeadHud>()?.IsDragging ?? false)) return;
        GetComponent<PlayerMovement>()?.CancelDash();
        attack?.CancelPath();
        drawingMode?.BeginDrawing(defensive);
        defensiveDrawing = defensive;
        isDrawing = true;
        drawingBlocked = false;
        points.Clear();
        lineRenderer.positionCount = 0;

        lineRenderer.startColor = lineRenderer.endColor = defensive ? new Color(0.3f, 0.85f, 1f) : new Color(1f, 0.8f, 0.3f);
        AddPoint(defensive ? GetMouseWorldPosition() : transform.position);
        if (!defensive) TryAddPoint(GetMouseWorldPosition());
    }

    private void DrawString()
    {
        if (!drawingBlocked) TryAddPoint(GetMouseWorldPosition());
        if (defensiveDrawing) projectileCapture?.SlowAlongString(points);
    }

    private void EndDrawing()
    {
        isDrawing = false;

        drawingMode?.EndDrawing();
        if (defensiveDrawing)
        {
            projectileCapture?.SlowAlongString(points);
            bool closed = projectileCapture != null && projectileCapture.TryCaptureClosedShape(points, out _);
            if (!closed && points.Count >= 2 && projectileCapture != null)
            {
                shields.RemoveAll(shield => shield == null);
                while (shields.Count >= Mathf.Max(1, maximumShields))
                {
                    Destroy(shields[0].gameObject);
                    shields.RemoveAt(0);
                }
                shields.Add(DefensiveStringShield.Create(projectileCapture, points, lineRenderer, shieldDuration));
            }
            ClearLine();
            return;
        }

        if (points.Count >= 2)
            attack?.FollowDrawnPath(points);
        if (attack == null || !attack.IsFollowingPath) ClearLine();
    }

    private void OnDisable()
    {
        isDrawing = false;
        drawingMode?.EndDrawing();
        foreach (DefensiveStringShield shield in shields)
            if (shield != null) Destroy(shield.gameObject);
        shields.Clear();
        ClearLine();
    }

    private void RefreshLine()
    {
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());
    }

    public void ShowRemainingPath(Vector2 playerPosition, List<Vector3> path, int nextPointIndex)
    {
        if (lineRenderer == null || path == null || nextPointIndex <= 0) return;

        int remainingPointCount = path.Count - nextPointIndex;
        if (remainingPointCount <= 0)
        {
            ClearLine();
            return;
        }

        lineRenderer.positionCount = remainingPointCount + 1;
        lineRenderer.SetPosition(0, new Vector3(playerPosition.x, playerPosition.y, 0f));

        for (int i = 0; i < remainingPointCount; i++)
            lineRenderer.SetPosition(i + 1, path[nextPointIndex + i]);
    }

    public void ClearLine()
    {
        points.Clear();
        if (lineRenderer != null)
            lineRenderer.positionCount = 0;
    }

    private void AddPoint(Vector3 point)
    {
        point.z = 0f;
        points.Add(point);
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPosition(points.Count - 1, point);
    }

    private void TryAddPoint(Vector3 point)
    {
        Vector3 lastPoint = points[points.Count - 1];
        if (Vector3.Distance(lastPoint, point) < minPointDistance) return;

        if (TryFindWall(lastPoint, point, out Vector3 stopPoint))
        {
            if (Vector3.Distance(lastPoint, stopPoint) >= minPointDistance)
                AddPoint(stopPoint);

            drawingBlocked = true;
            return;
        }

        AddPoint(point);
    }

    private bool TryFindWall(Vector3 from, Vector3 to, out Vector3 stopPoint)
    {
        stopPoint = to;
        RaycastHit2D[] hits = Physics2D.LinecastAll(from, to);
        RaycastHit2D closestHit = default;
        float closestDistance = float.MaxValue;

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == playerCollider || hit.collider.isTrigger) continue;
            if (hit.distance >= closestDistance) continue;

            closestHit = hit;
            closestDistance = hit.distance;
        }

        if (closestHit.collider == null) return false;

        Vector2 direction = ((Vector2)to - (Vector2)from).normalized;
        float playerRadius = playerCollider == null
            ? 0f
            : Mathf.Max(playerCollider.bounds.extents.x, playerCollider.bounds.extents.y);

        Vector2 safePoint = closestHit.point - direction * (playerRadius + wallPadding);
        if (Vector2.Dot(safePoint - (Vector2)from, direction) < 0f)
            safePoint = from;

        stopPoint = new Vector3(safePoint.x, safePoint.y, 0f);
        return true;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 screen = Mouse.current.position.ReadValue();
        Vector3 world = mainCamera.ScreenToWorldPoint(screen);
        world.z = 0f;
        if (defensiveDrawing && projectileCapture != null)
        {
            Vector2 delta = world - transform.position;
            world = transform.position + (Vector3)Vector2.ClampMagnitude(delta, projectileCapture.CaptureRange);
        }
        return world;
    }
}
