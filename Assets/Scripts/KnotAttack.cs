using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class KnotAttack : MonoBehaviour
{
    [Header("Path attack")]
    [SerializeField] private float pathSpeed = 12f;
    [SerializeField] private float attackRadius = 0.45f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float fallStartSpeed = 2f;
    [SerializeField, Min(0.1f)] private float minimumAttackPath = 0.75f;
    [SerializeField, Min(0.1f)] private float movementBeforeDamage = 0.4f;

    private readonly HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
    private Rigidbody2D rb;
    private PlayerMovement movement;
    private StringDrawer stringDrawer;
    private List<Vector3> path;
    private int pathIndex;
    private float savedGravityScale;
    private ProjectileStorage storage;
    private KarmaKeyword attackKeyword;
    private int keywordPower;
    private Vector2 attackStart;
    private bool movedForAttack;
    private bool useArmedOnMovement;
    private KarmaMovementEffect movementEffect;
    public bool HasMovedForAttack => movedForAttack;

    public bool IsFollowingPath { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement>();
        stringDrawer = GetComponent<StringDrawer>();
        storage = GetComponent<ProjectileStorage>();
        if (storage == null) storage = gameObject.AddComponent<ProjectileStorage>();
        movementEffect = GetComponent<KarmaMovementEffect>();
        if (movementEffect == null) movementEffect = gameObject.AddComponent<KarmaMovementEffect>();
    }

    private void FixedUpdate()
    {
        if (!IsFollowingPath || path == null) return;

        float remainingMove = pathSpeed * Time.fixedDeltaTime;
        while (remainingMove > 0f && pathIndex < path.Count)
        {
            Vector2 from = rb.position;
            Vector2 target = path[pathIndex];
            float distance = Vector2.Distance(from, target);
            bool isDrawnSegment = pathIndex > 0;

            if (distance <= remainingMove)
            {
                rb.position = target;
                remainingMove -= distance;
                pathIndex++;

                if (!isDrawnSegment)
                    CheckAttackAt(rb.position);
            }
            else
            {
                rb.position = Vector2.MoveTowards(from, target, remainingMove);
                remainingMove = 0f;
            }

            if (!movedForAttack && Vector2.Distance(attackStart, rb.position) >= movementBeforeDamage)
            {
                movedForAttack = true;
                if (useArmedOnMovement) storage.TryConsume(out attackKeyword, out keywordPower);
                movementEffect?.Begin(keywordPower > 0 ? (KarmaKeyword?)attackKeyword : null, keywordPower);
            }
            movementEffect?.SetMoving(Vector2.Distance(from, rb.position) > 0.001f);

            if (isDrawnSegment)
                CheckAttackBetween(from, rb.position);
        }

        if (pathIndex > 0)
            stringDrawer?.ShowRemainingPath(rb.position, path, pathIndex);

        if (pathIndex >= path.Count) FinishPath();
    }

    public void FollowDrawnPath(List<Vector3> drawnPath)
    {
        if (drawnPath == null || drawnPath.Count < 2) return;

        float length = 0f;
        float reach = 0f;
        Vector2 previous = rb.position;
        foreach (Vector3 point in drawnPath)
        {
            length += Vector2.Distance(previous, point);
            reach = Mathf.Max(reach, Vector2.Distance(rb.position, point));
            previous = point;
        }
        if (length < minimumAttackPath || reach < movementBeforeDamage) return;

        CancelPath();
        path = new List<Vector3>(drawnPath.Count);
        path.Add(rb.position);
        foreach (Vector3 point in drawnPath)
            if (Vector2.Distance(path[path.Count - 1], point) > 0.001f)
                path.Add(new Vector3(point.x, point.y, 0f));

        hitEnemies.Clear();
        keywordPower = 0;
        useArmedOnMovement = storage.HasArmedKeyword;
        storage.ReleaseAttackPreparationSlowMotion();
        attackStart = rb.position;
        movedForAttack = false;
        movementEffect?.Begin(null, 0);
        pathIndex = 0;
        savedGravityScale = rb.gravityScale;
        movement?.SetMovementLocked(true);
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.freezeRotation = true;
        IsFollowingPath = true;
    }

    // Backward compatibility for the existing Knot prefab.
    public void DashFromKnot(Vector3 knotPosition, List<Vector3> drawnPath)
    {
        FollowDrawnPath(drawnPath);
    }

    public void CancelPath()
    {
        if (!IsFollowingPath) return;
        FinishPath();
    }

    private void CheckAttackBetween(Vector2 from, Vector2 to)
    {
        Vector2 move = to - from;
        float distance = move.magnitude;

        if (distance > 0f)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                from,
                attackRadius,
                move.normalized,
                distance);

            foreach (RaycastHit2D hit in hits)
                TryHitEnemy(hit.collider);
        }

        CheckAttackAt(to);
    }

    private void CheckAttackAt(Vector2 position)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, attackRadius);
        foreach (Collider2D hit in hits)
            TryHitEnemy(hit);
    }

    private void TryHitEnemy(Collider2D hit)
    {
        Enemy enemy = hit.GetComponentInParent<Enemy>();
        if (!movedForAttack || enemy == null || !hitEnemies.Add(enemy)) return;

        float previousHealth = enemy.Health;
        enemy.TakeDamage(attackDamage);
        if (keywordPower > 0 && !enemy.IsDead)
            enemy.ApplyKeyword(attackKeyword, keywordPower, previousHealth - enemy.Health, GetComponent<PlayerHealth>());
        CameraShake.Instance?.Play(0.14f, 0.18f);
        HitStop.Instance?.Play(0.075f);
    }

    private void FinishPath()
    {
        IsFollowingPath = false;
        movementEffect?.Stop();
        rb.gravityScale = savedGravityScale;
        rb.linearVelocity = new Vector2(0f, -Mathf.Abs(fallStartSpeed));
        movement?.SetMovementLocked(false);
        stringDrawer?.ClearLine();
        path = null;
    }
}
