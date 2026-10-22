using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerHealth))]
public class GameRestartController : MonoBehaviour
{
    private PlayerHealth health;
    private GUIStyle buttonStyle;
    private Font font;
    public bool IsRestarting { get; private set; }
    public bool CanRestart => health != null && health.IsDead && !IsRestarting;

    private void Awake() { health = GetComponent<PlayerHealth>(); }

    private void Update()
    {
        if (CanRestart && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            TryRestart();
    }

    public bool TryRestart()
    {
        if (!CanRestart || string.IsNullOrEmpty(gameObject.scene.path)) return false;
        IsRestarting = true;
        GameTimeEffects.ResetForRestart();
        SceneManager.LoadSceneAsync(gameObject.scene.path, LoadSceneMode.Single);
        return true;
    }

    private void OnGUI()
    {
        if (!CanRestart) return;
        if (buttonStyle == null)
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 24 };
        }
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1000f, Screen.height / 600f), 0.5f, 2f);
        Rect button = new Rect(Screen.width * 0.5f - 120f * scale, Screen.height * 0.5f - 30f * scale,
            240f * scale, 60f * scale);
        if (GUI.Button(button, "다시 시작 (R)", buttonStyle)) TryRestart();
    }

    private void OnDestroy() { if (font != null) Destroy(font); }
}
