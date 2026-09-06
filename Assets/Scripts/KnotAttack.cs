using System.Collections.Generic;
using UnityEngine;

public class KnotAttack : MonoBehaviour
{
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float attackRadius = 0.3f;
    [SerializeField] private int attackDamage = 1;

    private Rigidbody2D rb;

    private bool isDashing = false;
    private bool hasReachedKnot = false;

    private Vector2 knotPosition;

    private List<Vector3> attackPath;
    private int currentPathIndex;

    private HashSet<Enemy> hitEnemies =
        new HashSet<Enemy>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (!isDashing)
            return;

        MoveAttack();
    }

    public void DashFromKnot(
        Vector3 knotPosition,
        List<Vector3> path)
    {
        if (path == null || path.Count < 4)
            return;

        this.knotPosition = knotPosition;

        hitEnemies.Clear();

        attackPath = RemoveLoops(path);

        if (attackPath.Count < 2)
            return;

        hasReachedKnot = false;

        currentPathIndex =
            FindPathIndexAfterKnot(
                knotPosition,
                attackPath
            );

        if (
            Vector2.Distance(
                rb.position,
                knotPosition
            ) < 0.1f
        )
        {
            rb.position = knotPosition;
            hasReachedKnot = true;
        }

        isDashing = true;

        Debug.Log(
            "고리 제거 완료 / String Action 시작"
        );
    }

    private List<Vector3> RemoveLoops(
        List<Vector3> originalPath)
    {
        List<Vector3> result =
            new List<Vector3>(originalPath);

        bool loopFound = true;

        while (loopFound)
        {
            loopFound = false;

            for (
                int i = 0;
                i < result.Count - 3;
                i++
            )
            {
                for (
                    int j = i + 2;
                    j < result.Count - 1;
                    j++
                )
                {
                    if (j == i + 1)
                        continue;

                    Vector2 intersection;

                    if (!GetSegmentIntersection(
                        result[i],
                        result[i + 1],
                        result[j],
                        result[j + 1],
                        out intersection))
                    {
                        continue;
                    }

                    List<Vector3> newPath =
                        new List<Vector3>();

                    for (int k = 0; k <= i; k++)
                    {
                        newPath.Add(result[k]);
                    }

                    newPath.Add(intersection);

                    for (
                        int k = j + 1;
                        k < result.Count;
                        k++
                    )
                    {
                        newPath.Add(result[k]);
                    }

                    result = newPath;

                    loopFound = true;

                    break;
                }

                if (loopFound)
                    break;
            }
        }

        return result;
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

        float crossRS =
            Cross(r, s);

        if (Mathf.Abs(crossRS) < 0.00001f)
            return false;

        Vector2 qMinusP =
            q - p;

        float t =
            Cross(
                qMinusP,
                s
            ) / crossRS;

        float u =
            Cross(
                qMinusP,
                r
            ) / crossRS;

        if (
            t >= 0f &&
            t <= 1f &&
            u >= 0f &&
            u <= 1f
        )
        {
            intersection =
                p + r * t;

            return true;
        }

        return false;
    }

    private float Cross(
        Vector2 a,
        Vector2 b)
    {
        return
            a.x * b.y -
            a.y * b.x;
    }

    private int FindPathIndexAfterKnot(
        Vector2 knot,
        List<Vector3> path)
    {
        int nearestIndex = -1;

        float nearestDistance =
            float.MaxValue;

        for (
            int i = 0;
            i < path.Count;
            i++
        )
        {
            float distance =
                Vector2.Distance(
                    knot,
                    path[i]
                );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        if (nearestIndex < 0)
            return path.Count;

        if (nearestIndex < path.Count - 1)
            return nearestIndex + 1;

        return path.Count;
    }

    private void MoveAttack()
    {
        if (!hasReachedKnot)
        {
            Vector2 direction =
                knotPosition -
                rb.position;

            float distance =
                direction.magnitude;

            float knotMoveDistance =
                dashSpeed *
                Time.deltaTime;

            if (distance <= knotMoveDistance)
            {
                rb.position =
                    knotPosition;

                hasReachedKnot = true;

                CheckAttack();

                return;
            }

            rb.position +=
                direction.normalized *
                knotMoveDistance;

            CheckAttack();

            return;
        }

        if (
            attackPath == null ||
            currentPathIndex >= attackPath.Count
        )
        {
            FinishAttack();
            return;
        }

        Vector2 target =
            attackPath[currentPathIndex];

        Vector2 directionToTarget =
            target - rb.position;

        float distanceToTarget =
            directionToTarget.magnitude;

        float pathMoveDistance =
            dashSpeed *
            Time.deltaTime;

        if (distanceToTarget <= pathMoveDistance)
        {
            rb.position =
                target;

            currentPathIndex++;

            CheckAttack();

            if (
                currentPathIndex >=
                attackPath.Count
            )
            {
                FinishAttack();
            }

            return;
        }

        rb.position +=
            directionToTarget.normalized *
            pathMoveDistance;

        CheckAttack();
    }

    private void CheckAttack()
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                rb.position,
                attackRadius
            );

        foreach (Collider2D hit in hits)
        {
            if (!hit.CompareTag("Enemy"))
                continue;

            Enemy enemy =
                hit.GetComponent<Enemy>();

            if (enemy == null)
                continue;

            if (hitEnemies.Contains(enemy))
                continue;

            hitEnemies.Add(enemy);

            enemy.TakeDamage(attackDamage);
        }
    }

    private void FinishAttack()
    {
        isDashing = false;

        Debug.Log(
            "String Action 종료!"
        );
    }
}