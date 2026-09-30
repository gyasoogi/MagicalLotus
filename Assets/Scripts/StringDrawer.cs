using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class StringDrawer : MonoBehaviour
{
    [SerializeField] public LineRenderer lineRenderer;
    [SerializeField] private float minPointDistance = 0.08f;
    [SerializeField] private float wallPadding = 0.05f;

    private readonly List<Vector3> points = new List<Vector3>();
    private Camera mainCamera;
    private KnotAttack attack;
    private ProjectileCapture projectileCapture;
    private DrawingModeController drawingMode;
    private Collider2D playerCollider;
    private bool isDrawing;
    private bool drawingBlocked;

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

        if (Mouse.current.leftButton.wasPressedThisFrame) StartDrawing();
        if (Mouse.current.leftButton.isPressed && isDrawing) DrawString();
        if (Mouse.current.leftButton.wasReleasedThisFrame && isDrawing) EndDrawing();
    }

    private void StartDrawing()
    {
        attack?.CancelPath();
        drawingMode?.BeginDrawing();
        isDrawing = true;
        drawingBlocked = false;
        points.Clear();
        lineRenderer.positionCount = 0;

        // The string begins where the player actually clicks.
        AddPoint(GetMouseWorldPosition());
    }

    private void DrawString()
    {
        if (drawingBlocked) return;
        TryAddPoint(GetMouseWorldPosition());
    }

    private void EndDrawing()
    {
        isDrawing = false;

        int capturedProjectileCount = 0;
        bool closedShape = projectileCapture != null
            && projectileCapture.TryCaptureClosedShape(points, out capturedProjectileCount);

        if (closedShape)
            RefreshLine();

        drawingMode?.EndDrawing();

        if (capturedProjectileCount > 0)
        {
            // A captured projectile consumes the loop, so the line disappears with it.
            ClearLine();
            return;
        }

        if (points.Count >= 2)
            attack?.FollowDrawnPath(points);
    }

    private void OnDisable()
    {
        isDrawing = false;
        drawingMode?.EndDrawing();
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
        return world;
    }
}
