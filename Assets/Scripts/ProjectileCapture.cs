using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ProjectileStorage))]
public class ProjectileCapture : MonoBehaviour
{
    [SerializeField] private float closeDistance = 0.7f;
    [SerializeField] private float minimumArea = 0.2f;
    [SerializeField, Min(0.1f)] private float captureRange = 12f;
    [SerializeField, Range(0.5f, 1f)] private float screenWidthFraction = 0.65f;
    [SerializeField, Min(0f)] private float energyPerBullet = 1f;
    public float CaptureRange
    {
        get
        {
            Camera camera = Camera.main;
            if (camera == null) return captureRange;
            float depth = Vector3.Dot(transform.position - camera.transform.position, camera.transform.forward);
            Vector3 left = camera.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth));
            Vector3 right = camera.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth));
            return Mathf.Max(captureRange, Vector3.Distance(left, right) * screenWidthFraction);
        }
    }
    public bool IsInRange(Vector2 position)
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        Vector2 center = body != null ? body.position : (Vector2)transform.position;
        return Vector2.Distance(center, position) <= CaptureRange;
    }

    private ProjectileStorage storage;

    private void Awake()
    {
        storage = GetComponent<ProjectileStorage>();
    }

    public bool TryCaptureClosedShape(List<Vector3> drawnPoints, out int capturedCount)
    {
        capturedCount = 0;

        if (!TryFindClosedShape(drawnPoints, out List<Vector3> closedShape))
            return false;

        // Show and use only the part of the line that actually made the loop.
        drawnPoints.Clear();
        drawnPoints.AddRange(closedShape);

        CapturableProjectile[] projectiles =
            FindObjectsByType<CapturableProjectile>(FindObjectsSortMode.None);

        List<CapturableProjectile> contained = new List<CapturableProjectile>();
        foreach (CapturableProjectile projectile in projectiles)
        {
            if (projectile.CanCapture && IsInRange(projectile.transform.position) && IsInsideShape(projectile.transform.position, closedShape))
                contained.Add(projectile);
        }
        capturedCount = storage.StoreBatch(contained);

        return true;
    }

    public void SlowAlongString(List<Vector3> points, bool allowDistantSlow = false)
    {
        if (points == null || points.Count < 2) return;
        foreach (var projectile in FindObjectsByType<CapturableProjectile>(FindObjectsSortMode.None))
        {
            if (!projectile.CanSlow) continue;
            bool inRange = IsInRange(projectile.transform.position);
            if (!inRange && !allowDistantSlow) continue;
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 closest = GetClosestPointOnSegment(projectile.transform.position, points[i - 1], points[i]);
                if (Vector2.Distance(closest, projectile.transform.position) > 0.2f) continue;
                projectile.SlowOnString();
                if (inRange && projectile.TryClaimEnergy()) storage.AddEnergy(energyPerBullet);
                break;
            }
        }
    }

    private bool TryFindClosedShape(List<Vector3> points, out List<Vector3> closedShape)
    {
        closedShape = null;
        if (points == null || points.Count < 3) return false;

        // Normal case: the end comes back near the beginning.
        if (Vector2.Distance(points[0], points[points.Count - 1]) <= closeDistance)
        {
            List<Vector3> startToEndLoop = new List<Vector3>(points);
            startToEndLoop.Add(points[0]);

            if (GetArea(startToEndLoop) >= minimumArea)
            {
                closedShape = startToEndLoop;
                return true;
            }
        }

        // Any newer part of the line may cross or overlap an older part.
        if (TryFindLatestCrossing(
                points,
                out int crossedSegment,
                out int closingSegment,
                out Vector3 crossingPoint))
        {
            List<Vector3> crossedLoop = BuildLoop(
                points,
                crossedSegment,
                closingSegment,
                crossingPoint);

            if (GetArea(crossedLoop) >= minimumArea)
            {
                closedShape = crossedLoop;
                return true;
            }
        }

        // A hand-drawn end rarely lands exactly on a line, so allow the same close distance.
        if (TryFindNearbySegment(points, out int nearbySegment, out Vector3 nearbyPoint))
        {
            List<Vector3> nearbyLoop = BuildLoop(
                points,
                nearbySegment,
                points.Count - 1,
                nearbyPoint);

            if (GetArea(nearbyLoop) >= minimumArea)
            {
                closedShape = nearbyLoop;
                return true;
            }
        }

        return false;
    }

    private bool TryFindLatestCrossing(
        List<Vector3> points,
        out int crossedSegment,
        out int closingSegment,
        out Vector3 crossingPoint)
    {
        crossedSegment = -1;
        closingSegment = -1;
        crossingPoint = Vector3.zero;
        float latestPositionOnLine = -1f;

        for (int newerSegment = 2; newerSegment < points.Count - 1; newerSegment++)
        {
            // Adjacent segments already share a point and do not make a loop.
            for (int olderSegment = 0; olderSegment <= newerSegment - 2; olderSegment++)
            {
                if (!TryGetSegmentMeeting(
                        points[olderSegment],
                        points[olderSegment + 1],
                        points[newerSegment],
                        points[newerSegment + 1],
                        out Vector2 meetingPoint,
                        out float amountOnNewerSegment))
                    continue;

                List<Vector3> possibleLoop = BuildLoop(
                    points,
                    olderSegment,
                    newerSegment,
                    meetingPoint);

                if (GetArea(possibleLoop) < minimumArea) continue;

                float positionOnLine = newerSegment + amountOnNewerSegment;
                if (positionOnLine <= latestPositionOnLine) continue;

                latestPositionOnLine = positionOnLine;
                crossedSegment = olderSegment;
                closingSegment = newerSegment;
                crossingPoint = meetingPoint;
            }
        }

        return crossedSegment >= 0;
    }

    private bool TryFindNearbySegment(
        List<Vector3> points,
        out int nearbySegment,
        out Vector3 nearbyPoint)
    {
        nearbySegment = -1;
        nearbyPoint = Vector3.zero;

        int lastIndex = points.Count - 1;
        Vector2 lineEnd = points[lastIndex];
        float closestDistance = closeDistance * closeDistance;

        for (int i = 0; i <= lastIndex - 3; i++)
        {
            Vector2 pointOnSegment = GetClosestPointOnSegment(lineEnd, points[i], points[i + 1]);
            float distance = (lineEnd - pointOnSegment).sqrMagnitude;
            if (distance > closestDistance) continue;

            List<Vector3> possibleLoop = BuildLoop(
                points,
                i,
                points.Count - 1,
                pointOnSegment);

            if (GetArea(possibleLoop) < minimumArea) continue;

            closestDistance = distance;
            nearbySegment = i;
            nearbyPoint = pointOnSegment;
        }

        return nearbySegment >= 0;
    }

    private List<Vector3> BuildLoop(
        List<Vector3> points,
        int touchedSegment,
        int lastPointToCopy,
        Vector3 meetingPoint)
    {
        List<Vector3> loop = new List<Vector3> { meetingPoint };

        for (int i = touchedSegment + 1; i <= lastPointToCopy; i++)
            loop.Add(points[i]);

        loop.Add(meetingPoint);
        return loop;
    }

    private bool TryGetSegmentMeeting(
        Vector2 firstStart,
        Vector2 firstEnd,
        Vector2 secondStart,
        Vector2 secondEnd,
        out Vector2 intersection,
        out float secondSegmentAmount)
    {
        intersection = Vector2.zero;
        secondSegmentAmount = 0f;

        Vector2 firstDirection = firstEnd - firstStart;
        Vector2 secondDirection = secondEnd - secondStart;
        float directionCross = Cross(firstDirection, secondDirection);

        if (Mathf.Abs(directionCross) < 0.0001f)
            return TryGetOverlappingPoint(
                firstStart,
                firstEnd,
                secondStart,
                secondEnd,
                out intersection,
                out secondSegmentAmount);

        Vector2 distanceBetweenStarts = secondStart - firstStart;
        float firstAmount = Cross(distanceBetweenStarts, secondDirection) / directionCross;
        float secondAmount = Cross(distanceBetweenStarts, firstDirection) / directionCross;

        if (firstAmount < 0f || firstAmount > 1f || secondAmount < 0f || secondAmount > 1f)
            return false;

        intersection = firstStart + firstDirection * firstAmount;
        secondSegmentAmount = secondAmount;
        return true;
    }

    private bool TryGetOverlappingPoint(
        Vector2 firstStart,
        Vector2 firstEnd,
        Vector2 secondStart,
        Vector2 secondEnd,
        out Vector2 overlapPoint,
        out float secondSegmentAmount)
    {
        overlapPoint = Vector2.zero;
        secondSegmentAmount = 0f;

        Vector2 firstDirection = firstEnd - firstStart;
        Vector2 secondDirection = secondEnd - secondStart;
        if (firstDirection.sqrMagnitude < 0.0001f || secondDirection.sqrMagnitude < 0.0001f)
            return false;

        // Parallel lines only overlap when both are on the same infinite line.
        if (Mathf.Abs(Cross(secondStart - firstStart, firstDirection)) > 0.0001f)
            return false;

        float firstStartOnSecond = Vector2.Dot(firstStart - secondStart, secondDirection)
            / secondDirection.sqrMagnitude;
        float firstEndOnSecond = Vector2.Dot(firstEnd - secondStart, secondDirection)
            / secondDirection.sqrMagnitude;

        float overlapStart = Mathf.Max(0f, Mathf.Min(firstStartOnSecond, firstEndOnSecond));
        float overlapEnd = Mathf.Min(1f, Mathf.Max(firstStartOnSecond, firstEndOnSecond));
        if (overlapStart > overlapEnd) return false;

        secondSegmentAmount = overlapStart;
        overlapPoint = secondStart + secondDirection * overlapStart;
        return true;
    }

    private Vector2 GetClosestPointOnSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude < 0.0001f) return start;

        float amount = Vector2.Dot(point - start, direction) / direction.sqrMagnitude;
        return start + direction * Mathf.Clamp01(amount);
    }

    private float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private float GetArea(List<Vector3> shape)
    {
        float area = 0f;

        for (int i = 0; i < shape.Count; i++)
        {
            Vector2 current = shape[i];
            Vector2 next = shape[(i + 1) % shape.Count];
            area += current.x * next.y - next.x * current.y;
        }

        return Mathf.Abs(area) * 0.5f;
    }

    private bool IsInsideShape(Vector2 point, List<Vector3> shape)
    {
        bool isInside = false;
        int vertexCount = shape.Count - 1;

        for (int i = 0, j = vertexCount - 1; i < vertexCount; j = i++)
        {
            Vector2 a = shape[i];
            Vector2 b = shape[j];

            bool crossesHeight = (a.y > point.y) != (b.y > point.y);
            if (!crossesHeight) continue;

            float crossX = (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
            if (point.x < crossX)
                isInside = !isInside;
        }

        return isInside;
    }
}
