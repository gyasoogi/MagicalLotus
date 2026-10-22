using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ProjectileStorage))]
[DefaultExecutionOrder(-200)]
public class KarmaBeadHud : MonoBehaviour
{
    private ProjectileStorage storage;
    private PlayerHealth health;
    private Texture2D circle;
    private GUIStyle label;
    private Font koreanFont;
    private int draggingSlot = -1;
    public bool IsDragging => draggingSlot >= 0;
    private void Awake()
    {
        storage = GetComponent<ProjectileStorage>();
        health = GetComponent<PlayerHealth>();
        circle = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                circle.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(31.5f - distance)));
            }
        circle.Apply();
        koreanFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);
    }

    public static Color KeywordColor(KarmaKeyword keyword)
    {
        switch (keyword)
        {
            case KarmaKeyword.Poison: return new Color(0.35f, 0.8f, 0.3f);
            case KarmaKeyword.Curse: return new Color(0.7f, 0.3f, 0.85f);
            case KarmaKeyword.Light: return new Color(1f, 0.85f, 0.35f);
            default: return new Color(0.3f, 0.45f, 0.85f);
        }
    }
    public static Color BeadColor(KarmaBead bead)
    {
        return bead.IsEmpty ? new Color(0.45f, 0.3f, 0.22f)
            : Color.Lerp(Color.black, KeywordColor(bead.keyword), bead.Freshness);
    }
    private static string KeywordName(KarmaKeyword keyword)
    {
        switch (keyword)
        {
            case KarmaKeyword.Poison: return "독";
            case KarmaKeyword.Curse: return "저주";
            case KarmaKeyword.Light: return "빛";
            default: return "어둠";
        }
    }
    private static float Scale => Mathf.Max(0.1f, Mathf.Min(Screen.width / 1000f, Screen.height / 600f));
    private static Rect SlotRect(int index, float width, float height)
    {
        float size = 48f + (3.5f - Mathf.Abs(index - 3.5f)) * 5f;
        return new Rect(width / 2f - 280f + index * 72f, height - 92f, size, size);
    }
    public Rect GetSlotScreenRect(int index)
    {
        Rect rect = SlotRect(index, Screen.width / Scale, Screen.height / Scale);
        return new Rect(rect.x * Scale, Screen.height - rect.yMax * Scale, rect.width * Scale, rect.height * Scale);
    }
    private static bool IsHudPoint(Vector2 point)
    {
        return point.y < 105f * Scale && Mathf.Abs(point.x - Screen.width * 0.5f) < 315f * Scale;
    }
    public static bool IsPointerOverHud()
    {
        if (Mouse.current == null) return false;
        Vector2 mouse = Mouse.current.position.ReadValue();
        return IsHudPoint(mouse);
    }
    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || health == null || health.IsDead) { draggingSlot = -1; return; }
        Vector2 point = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame)
        {
            draggingSlot = -1;
            for (int i = 0; i < storage.Beads.Count; i++)
            {
                if (!GetSlotScreenRect(i).Contains(point)) continue;
                KarmaBead bead = storage.Beads[i];
                if (!storage.HasArmedKeyword && storage.CanExtract(bead)) draggingSlot = i;
                break;
            }
        }
        if (!mouse.leftButton.wasReleasedThisFrame || !IsDragging) return;
        int slot = draggingSlot;
        draggingSlot = -1;
        Camera camera = Camera.main;
        if (camera == null || IsHudPoint(point) || !camera.pixelRect.Contains(point)) return;
        Ray ray = camera.ScreenPointToRay(point);
        Plane field = new Plane(Vector3.forward, Vector3.zero);
        if (field.Raycast(ray, out float distance)) storage.TryArmFromSlot(slot, ray.GetPoint(distance));
    }
    private void OnDisable()
    {
        draggingSlot = -1;
    }
    private void OnGUI()
    {
        if (storage == null) return;
        if (label == null)
            label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, font = koreanFont, fontSize = 15 };
        Matrix4x4 previous = GUI.matrix;
        Color previousColor = GUI.color;
        if (storage.IsExtractionActive)
        {
            Color tint = KeywordColor(storage.ArmedKeyword);
            tint.a = 0.28f * Mathf.Clamp01(storage.ExtractionRemaining / 0.35f);
            GUI.color = tint;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
        float scale = Mathf.Max(0.1f, Scale);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
        float width = Screen.width / scale, height = Screen.height / scale;
        Camera camera = Camera.main;
        if (camera != null)
            foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                if (enemy.IsDead) continue;
                Vector3 point = camera.WorldToScreenPoint(enemy.transform.position + Vector3.up * 1.1f);
                if (point.z <= 0f) continue;
                Rect statusRect = new Rect(point.x / scale - 40f, (Screen.height - point.y) / scale - 5f, 80f, 8f);
                GUI.Box(statusRect, GUIContent.none);
                GUI.color = new Color(0.85f, 0.2f, 0.2f);
                GUI.DrawTexture(new Rect(statusRect.x + 1f, statusRect.y + 1f,
                    78f * enemy.Health / enemy.MaxHealth, 6f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(statusRect.center.x - 80f, statusRect.y - 25f, 160f, 25f), enemy.StatusText, label);
            }
        GUI.Box(new Rect(width / 2f - 315f, height - 105f, 630f, 100f), GUIContent.none);
        for (int i = 0; i < storage.Beads.Count; i++)
        {
            KarmaBead bead = storage.Beads[i];
            Rect rect = SlotRect(i, width, height);
            float size = rect.width;
            if (!bead.IsEmpty)
            {
                float border = 1f + Mathf.Sqrt(bead.power) * 0.7f;
                bool warning = storage.OutbreakWarningActive && bead.IsRotten;
                bool nearRot = !bead.IsRotten && bead.Freshness <= 0.2f;
                GUI.color = warning || nearRot ? Color.Lerp(BeadColor(bead), Color.red,
                    0.5f + Mathf.Sin(Time.unscaledTime * (warning ? 16f : 8f)) * 0.4f) : BeadColor(bead);
                GUI.DrawTexture(new Rect(rect.x - border, rect.y - border, size + border * 2f, size + border * 2f), circle);
            }
            GUI.color = BeadColor(bead);
            if (draggingSlot == i) GUI.color *= new Color(1f, 1f, 1f, 0.45f);
            GUI.DrawTexture(rect, circle);
            GUI.color = Color.white;
            if (!bead.IsEmpty) GUI.Label(rect, KeywordName(bead.keyword), label);
        }
        if (storage.HasArmedKeyword && camera != null)
        {
            Vector3 point = camera.WorldToScreenPoint(storage.ArmedPosition);
            if (point.z > 0)
            {
                Rect marker = new Rect(point.x / scale - 25f, (Screen.height - point.y) / scale - 25f, 50f, 50f);
                GUI.color = KeywordColor(storage.ArmedKeyword);
                GUI.DrawTexture(marker, circle);
                GUI.color = Color.white;
                GUI.Label(marker, KeywordName(storage.ArmedKeyword), label);
            }
        }
        if (IsDragging && Mouse.current != null)
        {
            Vector2 point = Mouse.current.position.ReadValue();
            KarmaBead bead = storage.Beads[draggingSlot];
            Rect ghost = new Rect(point.x / scale - 27f, (Screen.height - point.y) / scale - 27f, 54f, 54f);
            GUI.color = BeadColor(bead);
            GUI.DrawTexture(ghost, circle);
            GUI.color = Color.white;
            GUI.Label(ghost, KeywordName(bead.keyword), label);
        }
        if (health != null)
        {
            GUI.Box(new Rect(15, 15, 210, 30), GUIContent.none);
            GUI.color = new Color(0.75f, 0.15f, 0.15f);
            GUI.DrawTexture(new Rect(19, 19, 202f * health.Health / health.MaxHealth, 22), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        GUI.Box(new Rect(15, 49, 210, 12), GUIContent.none);
        GUI.color = new Color(1f, 0.7f, 0.15f);
        GUI.DrawTexture(new Rect(18, 52, 204f * storage.EnergyFraction, 6), Texture2D.whiteTexture);
        GUI.color = previousColor;
        GUI.matrix = previous;
    }
    private void OnDestroy()
    {
        if (circle != null) Destroy(circle);
        if (koreanFont != null) Destroy(koreanFont);
    }
}
