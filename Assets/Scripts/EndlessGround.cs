using UnityEngine;

// A fixed pool of floor pieces recycles beyond the visible area as the player travels.
[DefaultExecutionOrder(-100)]
public class EndlessGround : MonoBehaviour
{
    [SerializeField, Range(5, 15)] private int segmentCount = 7;
    private Transform[] segments;
    private Transform original, originalParent, container;
    private Vector3 origin;
    private Rigidbody2D playerBody;
    private float width;
    private int centerIndex = int.MinValue;
    public int SegmentCount => segments == null ? 0 : segments.Length;
    public float SegmentWidth => width;
    public float SurfaceHeight { get; private set; }

    private void Awake()
    {
        GameObject floor = GameObject.Find("Floor");
        if (floor == null || floor.GetComponent<Collider2D>() == null) return;
        Collider2D collider = floor.GetComponent<Collider2D>();
        width = collider.bounds.size.x;
        if (width < 0.1f) return;
        SurfaceHeight = collider.bounds.max.y;
        original = floor.transform;
        originalParent = original.parent;
        origin = original.position;
        playerBody = GetComponent<Rigidbody2D>();
        container = new GameObject("Endless Ground").transform;
        int count = Mathf.Max(5, segmentCount);
        if (count % 2 == 0) count++;
        segments = new Transform[count];
        segments[0] = original;
        original.SetParent(container, true);
        for (int i = 1; i < count; i++)
        {
            segments[i] = Instantiate(original.gameObject, container).transform;
            segments[i].name = "Floor Tile " + i;
        }
        Recenter();
    }

    private void FixedUpdate() { Recenter(); }
    private void Recenter()
    {
        if (segments == null) return;
        float x = playerBody != null ? playerBody.position.x : transform.position.x;
        int center = Mathf.FloorToInt((x - origin.x) / width);
        if (center == centerIndex) return;
        centerIndex = center;
        int half = segments.Length / 2;
        for (int index = center - half; index <= center + half; index++)
        {
            int slot = ((index % segments.Length) + segments.Length) % segments.Length;
            segments[slot].position = new Vector3(origin.x + index * width, origin.y, origin.z);
        }
        Physics2D.SyncTransforms();
    }

    private void OnDestroy()
    {
        if (original != null)
        {
            original.SetParent(originalParent, true);
            original.position = origin;
        }
        if (container != null) Destroy(container.gameObject);
    }
}
