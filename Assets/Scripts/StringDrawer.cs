using System.Collections.Generic;
using UnityEngine;

public class StringDrawer : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private GameObject knotPrefab;
    [SerializeField] private float minPointDistance = 0.1f;

    private List<Vector3> points = new List<Vector3>();
    private Camera mainCamera;

    private bool isDrawing = false;
    private GameObject currentKnot;

    private void Awake()
    {
        mainCamera = Camera.main;
        lineRenderer.positionCount = 0;
    }

    private void Update()
    {
        // 선 그리기 시작
        if (Input.GetMouseButtonDown(0))
        {
            StartDrawing();
        }

        // 선 그리기
        if (Input.GetMouseButton(0) && isDrawing)
        {
            DrawString();
        }

        // 선 그리기 종료 + 매듭 생성
        if (Input.GetMouseButtonUp(0) && isDrawing)
        {
            EndDrawing();
        }

        // Space = 매듭 발동
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ActivateKnot();
        }
    }

    private void StartDrawing()
    {
        isDrawing = true;

        points.Clear();

        lineRenderer.positionCount = 0;

        AddPoint(GetMouseWorldPosition());
    }

    private void DrawString()
    {
        Vector3 mousePosition = GetMouseWorldPosition();

        if (points.Count == 0 ||
            Vector3.Distance(
                points[points.Count - 1],
                mousePosition
            ) >= minPointDistance)
        {
            AddPoint(mousePosition);
        }
    }

    private void EndDrawing()
    {
        isDrawing = false;

        // 기존 매듭 제거
        if (currentKnot != null)
        {
            Destroy(currentKnot);
            currentKnot = null;
        }

        // 교차점이 없으면 매듭을 만들지 않음
        Vector3? intersectionPoint = FindLastIntersection();

        if (!intersectionPoint.HasValue)
        {
            Debug.Log("교차점이 없습니다.");
            return;
        }

        // 교차점에 매듭 생성
        currentKnot = Instantiate(
            knotPrefab,
            intersectionPoint.Value,
            Quaternion.identity
        );

        Knot knot = currentKnot.GetComponent<Knot>();

        if (knot != null)
        {
            knot.SetStringPath(points);
        }

        Debug.Log("교차점에 매듭 생성!");
    }

    private void ActivateKnot()
    {
        if (currentKnot == null)
        {
            Debug.Log("발동할 매듭이 없습니다.");
            return;
        }

        Knot knot = currentKnot.GetComponent<Knot>();

        if (knot != null)
        {
            Debug.Log("매듭 발동!");
            knot.Activate();
        }
    }

    private void AddPoint(Vector3 point)
    {
        points.Add(point);

        lineRenderer.positionCount = points.Count;

        lineRenderer.SetPosition(
            points.Count - 1,
            point
        );
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;

        Vector3 worldPosition =
            mainCamera.ScreenToWorldPoint(mousePosition);

        worldPosition.z = 0f;

        return worldPosition;
    }

    private Vector3? FindLastIntersection()
    {
        if (points.Count < 4)
            return null;

        Vector3? lastIntersection = null;

        for (int i = 0; i < points.Count - 2; i++)
        {
            for (int j = i + 2; j < points.Count - 1; j++)
            {
                // 바로 이어지는 선분은 검사하지 않음
                if (j == i + 1)
                    continue;

                Vector2 intersection;

                if (GetSegmentIntersection(
                    points[i],
                    points[i + 1],
                    points[j],
                    points[j + 1],
                    out intersection))
                {
                    lastIntersection = intersection;
                }
            }
        }

        return lastIntersection;
    }

    private bool GetSegmentIntersection(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        out Vector2 intersection)
    {
        intersection = Vector2.zero;

        Vector2 p = a;
        Vector2 r = b - a;

        Vector2 q = c;
        Vector2 s = d - c;

        float crossRS = Cross(r, s);

        // 평행한 경우
        if (Mathf.Abs(crossRS) < 0.0001f)
            return false;

        Vector2 qMinusP = q - p;

        float t = Cross(qMinusP, s) / crossRS;
        float u = Cross(qMinusP, r) / crossRS;

        if (t >= 0f && t <= 1f &&
            u >= 0f && u <= 1f)
        {
            intersection = p + r * t;

            return true;
        }

        return false;
    }

    private float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }
}