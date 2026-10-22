#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Runs the actual scene in Play mode. Batch execution uses an isolated project copy.
[InitializeOnLoad]
public static class KarmaPlayValidation
{
    private const string Running = "MagicalLotus.KarmaValidation";
    private const string Batch = "MagicalLotus.KarmaValidation.Batch";
    private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private static readonly List<string> passed = new List<string>();
    private static readonly List<string> errors = new List<string>();
    private static readonly List<string> editorDiagnostics = new List<string>();
    private static int checks;
    private static double deadline;
    private static PlayerHealth player;
    private static ProjectileStorage storage;
    private static ProjectileCapture capture;
    private static EnemyRespawner spawner;
    private static CapturableProjectile projectilePrefab;
    private static Keyboard virtualKeyboard;
    private static Mouse virtualMouse;
    private static float originalTimeScale, originalFixedDeltaTime;
    private static InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
    private static InputSettings.BackgroundBehavior originalBackgroundBehavior;

    static KarmaPlayValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Pump;
    }

    [MenuItem("Tools/Magical Lotus/Run Karma Play Validation")]
    public static void RunInteractive() { Begin(false); }
    public static void RunBatch() { Begin(true); }
    private static void Begin(bool batch)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before validation.");
        if (!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        if (!Application.isBatchMode)
        {
            Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
        }
        SessionState.SetBool(Batch, batch);
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Running, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            originalTimeScale = Time.timeScale;
            originalFixedDeltaTime = Time.fixedDeltaTime;
            originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            checks = 0;
            passed.Clear(); errors.Clear(); editorDiagnostics.Clear(); routines.Clear();
            Application.logMessageReceived += OnLog;
            deadline = EditorApplication.timeSinceStartup + 180;
            routines.Push(Run());
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Running, false);
            if (SessionState.GetBool(Batch, false))
                EditorApplication.Exit(File.ReadAllText("Logs/karma-play-results.json").Contains("\"success\": true") ? 0 : 1);
        }
    }

    private static void Pump()
    {
        if (routines.Count == 0) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Play validation timed out.");
            IEnumerator top = routines.Peek();
            if (!top.MoveNext())
            {
                routines.Pop();
                if (routines.Count == 0) Finish();
            }
            else if (top.Current is IEnumerator nested) routines.Push(nested);
        }
        catch (Exception exception)
        {
            errors.Add(exception.ToString());
            Finish();
        }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        // Unity 6000.3's Search startup indexing can throw in a fresh headless project.
        // Preserve it separately so editor infrastructure is not reported as gameplay failure.
        if (stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
        { editorDiagnostics.Add(message + "\n" + stack); return; }
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + stack);
    }
    private static void Check(bool value, string name)
    {
        checks++;
        if (!value) throw new InvalidOperationException("FAIL: " + name);
        passed.Add(name);
        File.WriteAllText("Logs/karma-play-progress.txt", checks + ": " + name);
    }
    private static void Near(float actual, float expected, string name, float tolerance = 0.005f)
    { Check(Mathf.Abs(actual - expected) < tolerance, name + $" ({actual:0.000} / {expected:0.000})"); }
    private static void Set(object obj, string field, object value)
    { obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value); }
    private static object Get(object obj, string field)
    { return obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj); }
    private static void Call(object obj, string method, params object[] args)
    { obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args); }
    private static IEnumerator Wait(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }
    private static IEnumerator WaitReal(float seconds)
    {
        float until = Time.unscaledTime + seconds;
        while (Time.unscaledTime < until) yield return null;
    }
    private static IEnumerator FreshEnemy()
    {
        if (spawner.CurrentEnemy != null) spawner.CurrentEnemy.TakeDamage(10000);
        int count = spawner.SpawnCount;
        float until = Time.time + 3;
        while (spawner.SpawnCount == count && Time.time < until) yield return null;
        Check(spawner.CurrentEnemy != null && spawner.SpawnCount == count + 1, "Enemy respawned once");
        spawner.CurrentEnemy.GetComponent<EnemyProjectileThrower>().enabled = false;
    }
    private static CapturableProjectile Bullet(KarmaKeyword keyword, Vector2 position)
    {
        CapturableProjectile bullet = UnityEngine.Object.Instantiate(projectilePrefab, position, Quaternion.identity);
        bullet.SetKeyword(keyword);
        Rigidbody2D body = bullet.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0;
        return bullet;
    }
    private static List<Vector3> Loop(Vector2 center)
    {
        return new List<Vector3> { center + new Vector2(-1,-1), center + new Vector2(1,-1),
            center + new Vector2(1,1), center + new Vector2(-1,1), center + new Vector2(-1,-1) };
    }
    private static void ClearBeads() { foreach (var bead in storage.Beads) bead.Clear(); }
    private static void Pointer(KarmaBeadHud hud, Vector2 position, bool down, bool right = false)
    {
        InputSystem.QueueStateEvent(virtualMouse, new MouseState { position = position }.WithButton(right ? MouseButton.Right : MouseButton.Left, down));
        InputSystem.Update();
        virtualMouse.MakeCurrent();
        Call(hud, "Update");
        Call(player.GetComponent<StringDrawer>(), "Update");
    }
    private static void Load(KarmaKeyword keyword, int count = 1)
    {
        List<CapturableProjectile> bullets = new List<CapturableProjectile>();
        for (int i = 0; i < count; i++) bullets.Add(Bullet(keyword, new Vector2(50 + i * 0.01f, 50)));
        storage.AddEnergy(count * 3);
        int capacity = 8;
        foreach (var bead in storage.Beads)
            if (!bead.IsEmpty && !bead.IsRotten && bead.keyword == keyword) capacity -= bead.power;
        Check(storage.StoreBatch(bullets) == Mathf.Min(count, capacity), "Batch stored " + keyword + " x" + count);
    }

    private static Vector2 ScreenPoint(Vector2 world) { return Camera.main.WorldToScreenPoint(world); }
    private static Color RenderProbe(string filename, Vector2? screenSample = null)
    {
        Camera camera = Camera.main;
        RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 24, RenderTextureFormat.ARGB32);
        Texture2D frame = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            frame.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            frame.Apply();
            File.WriteAllBytes(Path.GetFullPath("Logs/" + filename + ".png"), frame.EncodeToPNG());
            Vector2 sample = screenSample ?? new Vector2(target.width * 0.7f, target.height * 0.7f);
            return frame.GetPixel(Mathf.Clamp(Mathf.FloorToInt(sample.x), 0, target.width - 1),
                Mathf.Clamp(Mathf.FloorToInt(sample.y), 0, target.height - 1));
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.Destroy(frame);
        }
    }

    private static IEnumerator CombatInputChecks()
    {
        ClearBeads();
        Set(storage, "energy", 0f);
        virtualMouse = InputSystem.AddDevice<Mouse>();
        KarmaBeadHud hud = player.GetComponent<KarmaBeadHud>();
        StringDrawer drawer = player.GetComponent<StringDrawer>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Vector2 origin = body.position;
        Enemy enemy = spawner.CurrentEnemy;
        Vector3 enemyOrigin = enemy.transform.position;
        float enemyHealth = enemy.Health;
        enemy.transform.position = origin + Vector2.right * 2;
        Physics2D.SyncTransforms();
        var untouched = Bullet(KarmaKeyword.Poison, origin + Vector2.right);
        Set(untouched, "contactDamage", 0f);
        Pointer(hud, ScreenPoint(origin), true);
        Pointer(hud, ScreenPoint(origin + Vector2.right * 4), true);
        Check(drawer.IsDrawing && !drawer.IsDefensiveDrawing && !untouched.IsSlowed && storage.Energy == 0,
            "Left string does not apply defensive contact slow or award energy");
        Near(Time.timeScale, 0.08f, "Left drawing restores world slow motion for enemies and bullets");
        Pointer(hud, ScreenPoint(origin + Vector2.right * 4), false);
        Check(player.GetComponent<KnotAttack>().IsFollowingPath, "Left release starts movement attack along drawn line");
        UnityEngine.Object.Destroy(untouched.gameObject);
        yield return Wait(0.6f);
        Near(enemy.Health, enemyHealth - 1, "Actual left drag damages enemy after player movement");
        Check(Vector2.Distance(body.position, origin) > 3.5f, "Left drag physically moves player along path");
        enemy.transform.position = enemyOrigin;
        body.position = origin; body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();

        ClearBeads(); Load(KarmaKeyword.Curse);
        Check(storage.TryArmFromSlot(0, origin), "Stationary attack test arms keyword");
        player.GetComponent<KnotAttack>().FollowDrawnPath(new List<Vector3> { origin, origin + Vector2.right * 0.01f, origin });
        Check(!player.GetComponent<KnotAttack>().IsFollowingPath && storage.HasArmedKeyword,
            "Tiny stationary path cannot attack or consume armed keyword");
        player.GetComponent<KnotAttack>().FollowDrawnPath(new List<Vector3> { origin, origin + Vector2.right });
        Near(Time.timeScale, 5, "Committing an armed path immediately releases preparation slow motion");
        player.GetComponent<KnotAttack>().CancelPath();
        Check(storage.HasArmedKeyword, "Cancelling before actual movement cannot discard an armed or warned ability");
        body.linearVelocity = Vector2.zero;
        storage.TryConsume(out _, out _);
        yield return WaitReal(3.15f);
        ClearBeads(); Set(storage, "energy", 0f);

        Camera camera = Camera.main;
        float originalSize = camera.orthographicSize;
        Check(capture.CaptureRange >= camera.orthographicSize * camera.aspect * 2f * 0.65f - 0.01f,
            "Defensive range covers at least sixty-five percent of visible screen width");
        camera.orthographicSize = originalSize * 3f;
        Near(capture.CaptureRange, camera.orthographicSize * camera.aspect * 2f * 0.65f,
            "Defensive drawing and extraction range expands when camera zooms out");
        camera.orthographicSize = originalSize;

        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
        probe.name = "Drawing grayscale render probe";
        probe.transform.position = camera.ViewportToWorldPoint(new Vector3(0.7f, 0.7f, 2f));
        Material probeMaterial = new Material(Shader.Find("Unlit/Color"));
        probeMaterial.color = new Color(1f, 0.1f, 0.1f);
        probe.GetComponent<Renderer>().sharedMaterial = probeMaterial;
        Color normalProbe = RenderProbe("karma-drawing-before");
        Check(normalProbe.r > normalProbe.g + 0.2f, "World renders in color before defensive drawing");
        Set(player.GetComponent<DrawingModeController>(), "screenEffect", null);
        Set(camera.GetComponent<DrawingScreenEffect>(), "grayscaleMaterial", null);

        var bullet = Bullet(KarmaKeyword.Poison, origin + Vector2.right);
        Set(bullet, "contactDamage", 0f);
        bullet.Launch(Vector2.right * 4);
        Vector2 bottom = origin + Vector2.right + Vector2.down;
        Vector2 top = origin + Vector2.right + Vector2.up;
        Pointer(hud, ScreenPoint(bottom), true, true);
        Pointer(hud, ScreenPoint(top), true, true);
        Check(drawer.IsDefensiveDrawing, "Right button draws defensive string");
        Near(Time.timeScale, 0.03f, "Right drawing slows world to three percent for enemies and bullets");
        Check(camera.GetComponent<DrawingScreenEffect>().IsActive, "Right drawing automatically connects and enables camera grayscale");
        Color slowProbe = RenderProbe("karma-drawing-slow");
        Check(Mathf.Abs(slowProbe.r - slowProbe.g) < 0.01f && Mathf.Abs(slowProbe.g - slowProbe.b) < 0.01f
            && slowProbe.r < normalProbe.r * 0.7f,
            "Actual camera output becomes dark grayscale even with initially missing effect and material references");
        Near(bullet.GetComponent<Rigidbody2D>().linearVelocity.x, 1, "Right string slows touched projectile to twenty-five percent");
        Near(storage.Energy, 1, "First string contact awards one energy");
        var timedThrower = enemy.GetComponent<EnemyProjectileThrower>();
        Set(timedThrower, "throwTimer", 2f);
        timedThrower.enabled = true;
        float slowStart = bullet.GetComponent<Rigidbody2D>().position.x;
        var untouchedBullet = Bullet(KarmaKeyword.Light, origin + Vector2.up * 2f);
        untouchedBullet.Launch(Vector2.right * 4f);
        float untouchedStart = untouchedBullet.GetComponent<Rigidbody2D>().position.x;
        float untouchedRealStart = Time.unscaledTime;
        yield return WaitReal(0.2f);
        Check((float)Get(timedThrower, "throwTimer") > 1.95f, "Enemy firing timer progresses slowly while drawing");
        float slowTravel = bullet.GetComponent<Rigidbody2D>().position.x - slowStart;
        Check(slowTravel > 0f && slowTravel < 0.1f, "Drawing slows actual bullet travel without fully stopping it");
        float untouchedTravel = untouchedBullet.GetComponent<Rigidbody2D>().position.x - untouchedStart;
        float untouchedSpeed = untouchedTravel / Mathf.Max(0.001f, Time.unscaledTime - untouchedRealStart);
        Check(!untouchedBullet.IsSlowed && untouchedSpeed > 0f && untouchedSpeed < 0.5f,
            $"Holding right mouse slows actual off-string projectile travel across the world ({untouchedSpeed:0.000} units/real second)");
        UnityEngine.Object.Destroy(untouchedBullet.gameObject);
        timedThrower.enabled = false;
        Pointer(hud, ScreenPoint(top), false, true);
        Near(Time.timeScale, 5, "Releasing defensive drawing restores previous game speed");
        Check(!camera.GetComponent<DrawingScreenEffect>().IsActive, "Releasing right mouse disables grayscale");
        Color restoredProbe = RenderProbe("karma-drawing-after");
        Check(restoredProbe.r > restoredProbe.g + 0.2f, "Actual camera output restores color on right release");
        foreach (var shield in UnityEngine.Object.FindObjectsByType<DefensiveStringShield>(FindObjectsSortMode.None))
            UnityEngine.Object.Destroy(shield.gameObject);
        UnityEngine.Object.Destroy(probe);
        UnityEngine.Object.Destroy(probeMaterial);
        Check(!drawer.IsDrawing && !player.GetComponent<KnotAttack>().IsFollowingPath,
            "Right release never starts a movement attack");
        Near(Vector2.Distance(body.position, origin), 0, "Defensive drawing does not teleport player");
        var line = new List<Vector3> { (Vector2)bullet.transform.position + Vector2.down, (Vector2)bullet.transform.position + Vector2.up };
        capture.SlowAlongString(line); capture.SlowAlongString(line);
        Near(storage.Energy, 1, "Same projectile cannot repeatedly award energy");
        Vector2 before = bullet.transform.position;
        yield return Wait(0.05f);
        Check(Vector2.Distance(before, bullet.transform.position) > 0 && bullet.gameObject.activeInHierarchy,
            "Slowed projectile keeps moving and remains present");
        var loop = Loop(bullet.transform.position);
        capture.TryCaptureClosedShape(loop, out int count);
        Check(count == 0 && bullet.CanCapture, "Insufficient energy cannot extract karma");
        storage.AddEnergy(2);
        capture.TryCaptureClosedShape(loop, out count);
        Check(count == 1 && bullet.KarmaExtracted && bullet.gameObject.activeInHierarchy && bullet.CanSlow,
            "Nearby extraction yields karma while leaving bullet active and dangerous");
        Near(storage.Energy, 0, "Karma extraction spends three energy per bullet");
        storage.AddEnergy(3);
        capture.TryCaptureClosedShape(loop, out count);
        Check(count == 0 && storage.Energy == 3, "Already extracted projectile cannot generate duplicate karma");
        UnityEngine.Object.Destroy(bullet.gameObject);

        var distant = Bullet(KarmaKeyword.Curse, origin + Vector2.right * (capture.CaptureRange + 1f));
        capture.SlowAlongString(new List<Vector3> { (Vector2)distant.transform.position + Vector2.down, (Vector2)distant.transform.position + Vector2.up });
        Check(!distant.IsSlowed && storage.Energy == 3, "Out-of-range string cannot slow bullets or farm energy");
        capture.TryCaptureClosedShape(Loop(distant.transform.position), out count);
        Check(count == 0 && distant.CanCapture && storage.Energy == 3, "Out-of-range closed loop cannot extract karma");
        UnityEngine.Object.Destroy(distant.gameObject);
        var withinReach = Bullet(KarmaKeyword.Curse, origin + Vector2.right * (capture.CaptureRange - 0.25f));
        capture.SlowAlongString(new List<Vector3> { withinReach.transform.position + Vector3.down, withinReach.transform.position + Vector3.up });
        Check(withinReach.IsSlowed && storage.Energy == 4, "Expanded defensive range slows distant bullets and grants energy");
        capture.TryCaptureClosedShape(Loop(withinReach.transform.position), out count);
        Check(count == 1 && withinReach.KarmaExtracted && withinReach.gameObject.activeInHierarchy,
            "Expanded defensive range extracts distant karma without removing the projectile");
        UnityEngine.Object.Destroy(withinReach.gameObject);
        Set(storage, "energy", 3f);
        Vector2 distantCursor = origin + Vector2.right * (capture.CaptureRange * 2f);
        Pointer(hud, ScreenPoint(distantCursor), true, true);
        var points = (List<Vector3>)Get(drawer, "points");
        Check(points.Count > 0 && Mathf.Abs(Vector2.Distance(points[0], origin) - capture.CaptureRange) <= 0.01f,
            "Defensive mouse drawing is clamped to capture range");
        Pointer(hud, ScreenPoint(distantCursor), false, true);

        var forbidden = Bullet(KarmaKeyword.Dark, origin + Vector2.right * 2);
        Set(forbidden, "contactDamage", 0f);
        forbidden.SetAbsorbable(false);
        capture.SlowAlongString(new List<Vector3> { (Vector2)forbidden.transform.position + Vector2.down, (Vector2)forbidden.transform.position + Vector2.up });
        Check(forbidden.IsSlowed && !forbidden.CanCapture, "Unabsorbable bullet can be slowed but cannot yield karma");
        capture.TryCaptureClosedShape(Loop(forbidden.transform.position), out count);
        Check(count == 0 && forbidden.gameObject.activeInHierarchy, "Red-ring bullet survives closed-loop extraction attempt");
        UnityEngine.Object.Destroy(forbidden.gameObject);
        var drifting = Bullet(KarmaKeyword.Light, origin + Vector2.right * 2);
        drifting.SlowOnString();
        yield return Wait(0.1f);
        Check(drifting.GetComponent<Rigidbody2D>().linearVelocity.magnitude >= 0.19f,
            "Even a zero-velocity slowed projectile has minimum drift");
        UnityEngine.Object.Destroy(drifting.gameObject);
        InputSystem.RemoveDevice(virtualMouse); virtualMouse = null;
        foreach (var shield in UnityEngine.Object.FindObjectsByType<DefensiveStringShield>(FindObjectsSortMode.None))
            UnityEngine.Object.Destroy(shield.gameObject);
        yield return null;
        ClearBeads();
    }

    private static IEnumerator OpenShieldChecks()
    {
        Time.timeScale = 1f;
        ClearBeads(); Set(storage, "energy", 0f);
        virtualMouse = InputSystem.AddDevice<Mouse>();
        KarmaBeadHud hud = player.GetComponent<KarmaBeadHud>();
        StringDrawer drawer = player.GetComponent<StringDrawer>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Vector2 origin = body.position;
        Vector2 center = origin + Vector2.right * 2;
        Pointer(hud, ScreenPoint(center + Vector2.down), true, true);
        Pointer(hud, ScreenPoint(center + Vector2.up), true, true);
        Pointer(hud, ScreenPoint(center + Vector2.up), false, true);
        var shields = UnityEngine.Object.FindObjectsByType<DefensiveStringShield>(FindObjectsSortMode.None);
        Check(shields.Length == 1 && shields[0].RemainingSeconds > 4.9f, "Released open right stroke creates a five-real-second shield");
        DefensiveStringShield shield = shields[0];
        Check(shield.GetComponent<LineRenderer>().positionCount == 2 && drawer.lineRenderer.positionCount == 0,
            "Shield keeps its own visible line independently of subsequent drawing");
        Near(Time.timeScale, 1, "Lingering shield never holds world slow motion");
        Check(!Camera.main.GetComponent<DrawingScreenEffect>().IsActive, "Lingering shield never holds grayscale");
        var bullet = Bullet(KarmaKeyword.Poison, center);
        bullet.Launch(Vector2.up * 0.2f);
        yield return WaitReal(1.1f);
        Check(shield != null && bullet.IsSlowed && bullet.gameObject.activeInHierarchy && bullet.CanCapture,
            "Released shield continues slowing live projectiles without consuming them");
        Near(storage.Energy, 1, "Persistent shield awards energy only once per projectile");
        Color shieldPixel = RenderProbe("karma-shield-preview", ScreenPoint(center + Vector2.up * 0.8f));
        Check(shieldPixel.g > shieldPixel.r + 0.1f && shieldPixel.b > shieldPixel.r + 0.1f,
            "Released shield remains visibly cyan in actual camera output");
        body.position = origin + Vector2.left * (capture.CaptureRange + 5f);
        body.linearVelocity = Vector2.zero;
        var lateBullet = Bullet(KarmaKeyword.Curse, center + Vector2.up * 0.5f);
        lateBullet.Launch(Vector2.up * 0.2f);
        yield return WaitReal(0.15f);
        Check(!capture.IsInRange(lateBullet.transform.position) && lateBullet.IsSlowed,
            "World shield protects its original location after the player moves away");
        Near(storage.Energy, 1, "Distant shield cannot farm out-of-range energy");
        body.position = origin; body.linearVelocity = Vector2.zero;
        yield return WaitReal(shield.RemainingSeconds + 0.15f);
        Check(shield == null, "Open shield expires on real time without retaining an invisible blocker");
        yield return Wait(0.5f);
        Check(!bullet.IsSlowed && !lateBullet.IsSlowed, "Bullets recover contact slow after shield expires");
        UnityEngine.Object.Destroy(bullet.gameObject); UnityEngine.Object.Destroy(lateBullet.gameObject);
        yield return null;

        Pointer(hud, ScreenPoint(center), true, true);
        Pointer(hud, ScreenPoint(center), false, true);
        Check(UnityEngine.Object.FindObjectsByType<DefensiveStringShield>(FindObjectsSortMode.None).Length == 0,
            "Single-point defensive click does not create a shield");
        var loop = Loop(center);
        Pointer(hud, ScreenPoint(loop[0]), true, true);
        for (int i = 1; i < loop.Count; i++) Pointer(hud, ScreenPoint(loop[i]), true, true);
        Pointer(hud, ScreenPoint(loop[loop.Count - 1]), false, true);
        Check(UnityEngine.Object.FindObjectsByType<DefensiveStringShield>(FindObjectsSortMode.None).Length == 0,
            "Closed defensive loop performs extraction without leaving an open shield");
        for (int i = 0; i < 4; i++)
        {
            Vector2 offset = Vector2.right * (i * 0.2f);
            Pointer(hud, ScreenPoint(center + offset + Vector2.down), true, true);
            Pointer(hud, ScreenPoint(center + offset + Vector2.up), true, true);
            Pointer(hud, ScreenPoint(center + offset + Vector2.up), false, true);
        }
        yield return null; yield return null;
        Check(UnityEngine.Object.FindObjectsByType<DefensiveStringShield>(FindObjectsSortMode.None).Length == 3,
            "Only three recent shields remain when the player draws repeatedly");
        Call(drawer, "OnDisable");
        yield return null; yield return null;
        Check(UnityEngine.Object.FindObjectsByType<DefensiveStringShield>(FindObjectsSortMode.None).Length == 0,
            "Disabling player drawing clears all lingering shield objects");
        Near(Time.timeScale, 1, "Shield cleanup leaves normal world speed");
        InputSystem.RemoveDevice(virtualMouse); virtualMouse = null;
        ClearBeads(); Time.timeScale = 5f;
    }

    private static IEnumerator MovementEffectChecks()
    {
        float previousScale = Time.timeScale;
        Time.timeScale = 0.5f;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Vector2 origin = body.position;
        foreach (KarmaKeyword keyword in Enum.GetValues(typeof(KarmaKeyword)))
        {
            ClearBeads(); Load(keyword);
            storage.TryArmFromSlot(0, origin);
            player.GetComponent<KnotAttack>().FollowDrawnPath(new List<Vector3> { origin, origin + Vector2.right * 2 });
            yield return Wait(0.06f);
            var effect = player.GetComponent<KarmaMovementEffect>();
            Check(effect.Keyword == keyword && effect.IsEmitting, keyword + " movement emits matching trail and particles");
            Check(Vector4.Distance(effect.GetComponentInChildren<TrailRenderer>().startColor, KarmaBeadHud.KeywordColor(keyword)) < 0.01f,
                keyword + " trail uses keyword color");
            if (keyword == KarmaKeyword.Light) ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/karma-movement-preview.png"));
            yield return Wait(0.3f);
            Check(!effect.IsEmitting, keyword + " movement trail stops after attack");
            body.position = origin; body.linearVelocity = Vector2.zero;
            Check(!storage.IsExtractionActive, keyword + " attack ends extraction effects immediately after consumption");
            yield return null;
        }
        Time.timeScale = previousScale;
    }

    private static IEnumerator EndlessGroundChecks()
    {
        EndlessGround ground = player.GetComponent<EndlessGround>();
        Check(ground != null && ground.SegmentCount == 7, "Endless ground initializes a fixed seven-tile pool");
        Transform container = (Transform)Get(ground, "container");
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Vector2 originalPosition = body.position;
        float originalGravity = body.gravityScale;
        float previousScale = Time.timeScale;
        Time.timeScale = 5;
        foreach (float x in new[] { -10000f, 10000f })
        {
            body.position = new Vector2(x, ground.SurfaceHeight + 2f);
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 3;
            Call(ground, "FixedUpdate");
            var hits = Physics2D.RaycastAll(new Vector2(x, ground.SurfaceHeight + 1f), Vector2.down, 2f);
            Check(Array.Exists(hits, hit => hit.collider.transform.IsChildOf(container)), "Ground collision exists at x=" + x);
            yield return Wait(0.8f);
            float feet = player.GetComponent<Collider2D>().bounds.min.y;
            Check(feet >= ground.SurfaceHeight - 0.06f && feet < ground.SurfaceHeight + 0.1f,
                "Player can physically stand at x=" + x);
            Check(container.childCount == 7, "Long-distance travel recycles tiles without creating extra floor objects");
        }
        float start = ground.SegmentWidth * 0.5f - 1f;
        body.position = new Vector2(start, ground.SurfaceHeight + player.GetComponent<Collider2D>().bounds.extents.y + 0.04f);
        body.linearVelocity = Vector2.right * 6;
        Call(ground, "FixedUpdate");
        yield return Wait(5f);
        Check(body.position.x > start + 20, "Player traverses multiple floor joins while ground recycles");
        Check(player.GetComponent<Collider2D>().bounds.min.y >= ground.SurfaceHeight - 0.06f,
            "Crossing floor joins never drops player through a gap");
        Check(container.childCount == 7, "Floor object count stays bounded during continuous travel");
        body.position = originalPosition; body.linearVelocity = Vector2.zero; body.gravityScale = originalGravity;
        Call(ground, "FixedUpdate");
        Time.timeScale = previousScale;
    }

    private static void Keys(params Key[] keys)
    {
        InputSystem.QueueStateEvent(virtualKeyboard, new KeyboardState(keys));
        InputSystem.Update();
        virtualKeyboard.MakeCurrent();
        Call(player.GetComponent<PlayerMovement>(), "Update");
    }

    private static IEnumerator DashChecks()
    {
        float previousScale = Time.timeScale;
        Time.timeScale = 1f;
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Vector2 origin = body.position;
        float gravity = body.gravityScale;
        float enemyHealth = spawner.CurrentEnemy.Health;
        movement.enabled = true;
        body.gravityScale = 0;
        body.linearVelocity = Vector2.up * 1.5f;
        virtualKeyboard = InputSystem.AddDevice<Keyboard>();
        Keys(Key.A, Key.LeftShift);
        Check(movement.IsDashing && body.linearVelocity.x < -17f, "A plus left Shift starts a leftward dash");
        Near(body.linearVelocity.y, 1.5f, "Dash preserves vertical jump or fall velocity");
        Check(!movement.TryDash(), "Repeated Shift cannot stack or restart an active dash");
        Keys(Key.LeftShift);
        yield return Wait(0.25f);
        Check(!movement.IsDashing && origin.x - body.position.x > 2f && origin.x - body.position.x < 3.7f,
            "Dash moves the actual player about three units and ends");
        Near(body.linearVelocity.x, 0, "Dash releases horizontal velocity when no movement key is held");
        Check(movement.DashCooldownRemaining > 0f && !movement.TryDash(), "Dash cooldown prevents immediate reuse");
        Vector2 stopped = body.position;
        yield return Wait(movement.DashCooldownRemaining + 0.08f);
        Check(!movement.IsDashing && Mathf.Abs(body.position.x - stopped.x) < 0.02f,
            "Holding Shift through cooldown does not automatically dash again");
        Keys(); Keys(Key.RightShift);
        Check(movement.IsDashing && body.linearVelocity.x < -17f,
            "Right Shift also dashes and idle dash uses last facing direction");
        movement.SetMovementLocked(true);
        Check(!movement.IsDashing && !movement.TryDash() && body.linearVelocity == Vector2.zero,
            "Path or death movement lock cancels and blocks dash");
        movement.SetMovementLocked(false); Keys(); body.linearVelocity = Vector2.zero;
        yield return Wait(movement.DashCooldownRemaining + 0.04f);

        body.position = origin; body.linearVelocity = Vector2.zero;
        GameObject wall = new GameObject("Dash collision validation wall");
        wall.transform.position = origin + Vector2.right * 2f;
        BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();
        wallCollider.size = new Vector2(0.3f, 4f);
        Physics2D.SyncTransforms();
        Keys(Key.D, Key.LeftShift); Keys();
        yield return Wait(0.25f);
        Check(body.position.x > origin.x + 0.5f
            && player.GetComponent<Collider2D>().bounds.max.x <= wallCollider.bounds.min.x + 0.05f,
            "Rightward dash collides with walls instead of passing through them");
        UnityEngine.Object.Destroy(wall);
        yield return null;
        yield return Wait(movement.DashCooldownRemaining + 0.04f);
        body.position = origin; body.linearVelocity = Vector2.zero;
        Keys(Key.D, Key.RightShift);
        virtualMouse = InputSystem.AddDevice<Mouse>();
        KarmaBeadHud hud = player.GetComponent<KarmaBeadHud>();
        Vector2 pointer = ScreenPoint(origin + Vector2.up * 2);
        Pointer(hud, pointer, true, true);
        Check(player.GetComponent<StringDrawer>().IsDefensiveDrawing && !movement.IsDashing && !movement.TryDash(),
            "Starting defensive drawing cancels dash and blocks dashing while drawing");
        Pointer(hud, pointer, false, true);
        Near(Time.timeScale, 1, "Dash and defensive drawing leave no stuck world slow motion");
        Keys(Key.R);
        GameRestartController restart = player.GetComponent<GameRestartController>();
        Call(restart, "Update");
        Check(!restart.CanRestart && !restart.IsRestarting && !restart.TryRestart(),
            "R key and restart action cannot restart a living player");
        Near(spawner.CurrentEnemy.Health, enemyHealth, "Dash alone never attacks or damages enemies");
        Keys();
        movement.enabled = false;
        Check(!movement.TryDash(), "Disabled movement cannot dash");
        body.position = origin; body.linearVelocity = Vector2.zero; body.gravityScale = gravity;
        InputSystem.RemoveDevice(virtualKeyboard); virtualKeyboard = null;
        InputSystem.RemoveDevice(virtualMouse); virtualMouse = null;
        Time.timeScale = previousScale;
    }

    private static IEnumerator AwaitRestart(PlayerHealth previousPlayer)
    {
        double until = EditorApplication.timeSinceStartup + 10;
        do
        {
            yield return null;
            player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
            spawner = UnityEngine.Object.FindFirstObjectByType<EnemyRespawner>();
        } while ((player == null || ReferenceEquals(player, previousPlayer) || spawner == null)
            && EditorApplication.timeSinceStartup < until);
        Check(player != null && !ReferenceEquals(player, previousPlayer) && spawner != null,
            "Restart actually reloads the scene with a new player and spawner");
        storage = player.GetComponent<ProjectileStorage>();
        capture = player.GetComponent<ProjectileCapture>();
        foreach (Enemy enemy in spawner.AliveEnemies) enemy.GetComponent<EnemyProjectileThrower>().enabled = false;
        yield return null; yield return null;
        Near(player.Health, 100, "Restart restores starting player health");
        Check(!player.IsDead && !player.GetComponent<PlayerMovement>().MovementLocked
            && player.GetComponent<StringDrawer>().enabled, "Restart restores movement and drawing");
        Check(!player.GetComponent<PlayerMovement>().IsDashing && player.GetComponent<PlayerMovement>().DashCooldownRemaining == 0,
            "Restart clears dash and cooldown state");
        Check(storage.StoredProjectileCount == 0 && storage.Energy == 0 && !storage.HasArmedKeyword && !storage.IsExtractionActive,
            "Restart clears beads energy and pending karma");
        Check(spawner.AliveCount == 2 && UnityEngine.Object.FindObjectsByType<GameRestartController>(FindObjectsSortMode.None).Length == 1,
            "Restart recreates exactly two enemies and one restart controller");
        Check(UnityEngine.Object.FindObjectsByType<CapturableProjectile>(FindObjectsSortMode.None).Length == 0
            && UnityEngine.Object.FindObjectsByType<DefensiveStringShield>(FindObjectsSortMode.None).Length == 0
            && UnityEngine.Object.FindObjectsByType<KarmaOutbreak>(FindObjectsSortMode.None).Length == 0,
            "Restart clears old bullets shields and outbreak orbs");
        Near(Time.timeScale, 1, "Restart restores normal world speed");
        Near(Time.fixedDeltaTime, originalFixedDeltaTime, "Restart restores the original physics timestep");
        Check(player.GetComponent<EndlessGround>().SegmentCount == 7,
            "Restart recreates the seven-tile endless floor");
    }

    private static IEnumerator RestartChecks()
    {
        GameRestartController restart = player.GetComponent<GameRestartController>();
        Check(restart != null && restart.CanRestart, "Death makes restart available");
        player.GetComponent<PlayerMovement>().enabled = true;
        Check(!player.GetComponent<PlayerMovement>().TryDash(), "Dead player cannot dash");
        object staleOwner = new object();
        GameTimeEffects.Request(staleOwner, 0.03f);
        HitStop.Instance.Play(0.075f);
        PlayerHealth previousPlayer = player;
        virtualKeyboard = InputSystem.AddDevice<Keyboard>();
        Keys(Key.R);
        Call(restart, "Update");
        Check(restart.IsRestarting && !restart.TryRestart(), "R starts one asynchronous restart and duplicate requests are blocked");
        yield return AwaitRestart(previousPlayer);
        GameTimeEffects.Release(staleOwner);
        Near(Time.timeScale, 1, "Old time-effect owners cannot restore stale slowdown after reload");
        Check(!player.GetComponent<GameRestartController>().CanRestart,
            "Restart button disappears for the new living player");
        Keys(); Keys(Key.D, Key.LeftShift);
        Check(player.GetComponent<PlayerMovement>().IsDashing, "Shift dash works immediately after restart");
        Keys();
        Vector2 before = player.GetComponent<Rigidbody2D>().position;
        yield return Wait(0.08f);
        Check(player.GetComponent<Rigidbody2D>().position.x > before.x + 0.5f,
            "Restarted player physically moves when dashing");
        previousPlayer = player;
        player.TakeDamage(10000);
        restart = player.GetComponent<GameRestartController>();
        Check(restart.TryRestart(), "Restart button action also works after a second death");
        yield return AwaitRestart(previousPlayer);
        InputSystem.RemoveDevice(virtualKeyboard); virtualKeyboard = null;
    }

    private static IEnumerator Run()
    {
        yield return null; yield return null;
        player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        storage = player.GetComponent<ProjectileStorage>();
        capture = player.GetComponent<ProjectileCapture>();
        spawner = UnityEngine.Object.FindFirstObjectByType<EnemyRespawner>();
        projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CapturableProjectile.prefab").GetComponent<CapturableProjectile>();
        Check(player != null && storage != null && capture != null && spawner != null, "Main scene runtime initialization");
        Check(player.GetComponent<KarmaBeadHud>() != null && storage.Beads.Count == 8, "HUD and eight bead slots");
        Check(player.GetComponent<GameRestartController>() != null, "Death restart is connected automatically");
        Near(player.Health, 100, "Player starts at one hundred health");
        Near(player.MaxHealth, 200, "Player health ceiling is two hundred");
        player.HealMissingFraction(2f);
        Near(player.Health, 200, "Healing grows health up to two hundred and never overflows");
        Check(spawner.AliveCount == 2, "Two active enemies initially");
        Check(spawner.CurrentEnemy.GetComponent<EnemyProjectileThrower>().ProjectilesPerVolley == 4,
            "Reduced enemy volley defaults to four bullets");
        Near(spawner.CurrentEnemy.GetComponent<EnemyProjectileThrower>().ThrowInterval, 3f, "Enemy firing interval is reduced to one volley every three seconds");
        Transform expectedTarget = (Transform)Get(spawner.CurrentEnemy.GetComponent<EnemyProjectileThrower>(), "target");
        player.GetComponent<PlayerMovement>().enabled = false;
        player.GetComponent<PlayerJump>().enabled = false;
        player.GetComponent<StringDrawer>().enabled = false;
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        playerBody.gravityScale = 0; playerBody.linearVelocity = Vector2.zero;
        playerBody.position = new Vector2(-8, 5);
        foreach (Enemy activeEnemy in spawner.AliveEnemies) activeEnemy.GetComponent<EnemyProjectileThrower>().enabled = false;
        foreach (var bullet in UnityEngine.Object.FindObjectsByType<CapturableProjectile>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(bullet.gameObject);
        Time.timeScale = 5f;

        // Actual closed-loop geometry and batch strength.
        ClearBeads();
        Vector2 center = playerBody.position + Vector2.right * 1.5f;
        var inside = Bullet(KarmaKeyword.Poison, center);
        Bullet(KarmaKeyword.Poison, center + Vector2.right * 0.1f);
        var outside = Bullet(KarmaKeyword.Curse, center + Vector2.right * 3f);
        var loop = Loop(center);
        storage.AddEnergy(6);
        Check(capture.TryCaptureClosedShape(loop, out int count) && count == 2, "Closed string captures only contained bullets");
        Check(!inside.CanCapture && inside.KarmaExtracted && inside.gameObject.activeInHierarchy && inside.CanSlow
            && outside.CanCapture && storage.Beads[0].power == 2, "Karma extraction preserves live dangerous bullets and combines keywords");
        Check(!capture.TryCaptureClosedShape(new List<Vector3> { Vector3.zero, Vector3.right }, out _), "Open string rejects capture");
        UnityEngine.Object.Destroy(outside.gameObject);
        UnityEngine.Object.Destroy(inside.gameObject);
        Check(!storage.TryConsume(out _, out _), "Selecting a bead alone does not arm the attack");
        Check(storage.TryArmFromSlot(0, Vector3.zero), "Valid field drop arms selected bead");
        Near(Time.timeScale, 0.2f, "Extracting a bead slows preparation to twenty percent");
        Check(storage.TryConsume(out KarmaKeyword key, out int power) && key == KarmaKeyword.Poison && power == 2, "Selected bead consumes with correct keyword and power");
        Check(!storage.TryConsume(out _, out _), "Empty bead cannot consume twice");
        Near(Time.timeScale, 5, "Consuming karma immediately restores prior time scale");
        Check(!storage.IsExtractionActive, "Consuming karma immediately ends extraction tint and timer");
        ClearBeads(); Load(KarmaKeyword.Poison, 10);
        Check(storage.Beads[0].power == 8, "Multi-capture strength caps at eight");
        ClearBeads(); Load(KarmaKeyword.Poison, 2);
        storage.Beads[0].remaining = 10;
        Color oldColor = KarmaBeadHud.BeadColor(storage.Beads[0]);
        Load(KarmaKeyword.Poison, 3);
        Check(storage.Beads[0].power == 5 && storage.Beads[1].IsEmpty, "Same keyword merges across separate captures and increases strength");
        Near(storage.Beads[0].Freshness, 1, "Merging refreshes bead lifetime");
        Check(KarmaBeadHud.BeadColor(storage.Beads[0]).grayscale > oldColor.grayscale, "Refreshed bead regains its color");
        storage.Beads[0].remaining = 10;
        Near(KarmaBeadHud.BeadColor(storage.Beads[0]).g, KarmaBeadHud.KeywordColor(KarmaKeyword.Poison).g * 0.5f, "Half lifetime renders half brightness");
        storage.Beads[0].remaining = 0;
        Check(KarmaBeadHud.BeadColor(storage.Beads[0]) == Color.black, "Expired bead becomes black");
        Load(KarmaKeyword.Poison);
        Check(storage.Beads[0].IsRotten && storage.Beads[1].power == 1, "New acquisition does not revive a rotten bead");
        ClearBeads();
        foreach (KarmaKeyword kind in Enum.GetValues(typeof(KarmaKeyword))) Load(kind);
        Check(storage.Beads[0].keyword == KarmaKeyword.Poison && storage.Beads[1].keyword == KarmaKeyword.Curse
            && storage.Beads[2].keyword == KarmaKeyword.Light && storage.Beads[3].keyword == KarmaKeyword.Dark, "All keyword variants occupy separate beads");

        // Real Input System events, then paused/elapsed decay.
        virtualKeyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.QueueStateEvent(virtualKeyboard, new KeyboardState(Key.Digit4));
        InputSystem.Update();
        virtualKeyboard.MakeCurrent();
        Check(virtualKeyboard.digit4Key.isPressed, "Virtual keyboard receives queued digit state");
        Call(storage, "Update");
        Check(!storage.HasArmedKeyword && !player.GetComponent<KarmaBeadHud>().IsDragging
            && typeof(ProjectileStorage).GetProperty("SelectedSlot") == null, "Number keys no longer select or activate abilities");
        InputSystem.QueueStateEvent(virtualKeyboard, new KeyboardState());
        InputSystem.Update();
        InputSystem.RemoveDevice(virtualKeyboard); virtualKeyboard = null;
        ClearBeads(); Load(KarmaKeyword.Light);
        float beforePause = storage.Beads[0].remaining;
        Time.timeScale = 0;
        double resume = EditorApplication.timeSinceStartup + 0.2;
        while (EditorApplication.timeSinceStartup < resume) yield return null;
        Near(storage.Beads[0].remaining, beforePause, "Paused game does not decay beads");
        Time.timeScale = 5;
        yield return Wait(20.2f);
        Check(storage.Beads[0].IsRotten && !storage.Beads[0].IsEmpty, "Twenty seconds makes bead rotten without freeing slot");
        Check(!storage.TryConsume(out _, out _), "Rotten bead cannot activate");
        Check(!storage.TryArmFromSlot(0, Vector3.zero), "Rotten bead cannot be dropped to arm");
        Load(KarmaKeyword.Curse);
        Check(storage.Beads[1].keyword == KarmaKeyword.Curse && !storage.Beads[1].IsEmpty, "Rotten slot is not overwritten");

        ClearBeads();
        for (int i = 0; i < 8; i++) { Load(KarmaKeyword.Curse); storage.Beads[i].remaining = 0; }
        var overflow = Bullet(KarmaKeyword.Poison, center);
        Check(storage.StoreBatch(new List<CapturableProjectile> { overflow }) == 0 && overflow.CanCapture, "Full inventory preserves uncaptured overflow bullets");
        storage.Beads[7].remaining = 10;
        Load(KarmaKeyword.Curse);
        Check(storage.Beads[7].power == 2, "Matching ability merges even when every bead slot is occupied");
        UnityEngine.Object.Destroy(overflow.gameObject);
        ClearBeads();

        // Exercise the actual HUD mouse handler and StringDrawer together.
        virtualMouse = InputSystem.AddDevice<Mouse>();
        KarmaBeadHud hud = player.GetComponent<KarmaBeadHud>();
        Load(KarmaKeyword.Poison, 2);
        Vector2 fieldPoint = new Vector2(Screen.width * 0.6f, Screen.height * 0.6f);
        Pointer(hud, hud.GetSlotScreenRect(0).center, true);
        Check(hud.IsDragging && !(bool)Get(player.GetComponent<StringDrawer>(), "isDrawing"), "Dragging a bead does not start string drawing");
        Pointer(hud, fieldPoint, true);
        Pointer(hud, fieldPoint, false);
        Check(!hud.IsDragging && storage.HasArmedKeyword && storage.ArmedPower == 2 && storage.Beads[0].IsEmpty,
            "Field drag/drop arms keyword and frees bead slot");
        Vector3 projected = Camera.main.WorldToScreenPoint(storage.ArmedPosition);
        Near(Vector2.Distance(projected, fieldPoint), 0, "Dropped marker matches field pointer", 0.1f);
        Check(!(bool)Get(player.GetComponent<StringDrawer>(), "isDrawing") && !player.GetComponent<KnotAttack>().IsFollowingPath,
            "Dropping a bead does not trigger an attack");
        player.GetComponent<KnotAttack>().FollowDrawnPath(new List<Vector3> { Vector3.zero });
        Check(storage.HasArmedKeyword, "Invalid path does not consume armed keyword");
        Load(KarmaKeyword.Curse);
        Check(!storage.TryArmFromSlot(0, Vector3.zero) && !storage.Beads[0].IsEmpty && storage.ArmedKeyword == KarmaKeyword.Poison,
            "Existing armed keyword cannot be overwritten or waste another bead");
        Check(storage.IsExtractionActive && storage.ArmedKeyword == KarmaKeyword.Poison, "Extraction exposes correct color keyword");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/karma-extraction-preview.png"));
        yield return WaitReal(3.15f);
        Check(!storage.HasArmedKeyword && !storage.IsExtractionActive && !storage.TryConsume(out _, out _), "Unused extracted ability disappears after three real seconds");
        Near(Time.timeScale, 5, "Timed-out extraction restores normal test speed");
        Check(storage.TryArmFromSlot(0, Vector3.zero), "Another ability can be extracted after timeout");
        Check(storage.TryConsume(out key, out power) && key == KarmaKeyword.Curse && power == 1, "Next attack consumes extracted ability once");
        yield return WaitReal(3.15f);

        // Nested slow motion must restore the remaining owner, rather than a stale time scale.
        ClearBeads(); Load(KarmaKeyword.Dark);
        storage.TryArmFromSlot(0, Vector3.zero);
        DrawingModeController drawing = player.GetComponent<DrawingModeController>();
        drawing.BeginDrawing();
        Near(Time.timeScale, 0.08f, "Drawing slow motion takes priority over extraction");
        HitStop.Instance.Play(0.075f);
        Near(Time.timeScale, 0, "Hit stop takes priority over both slow effects");
        yield return WaitReal(0.15f);
        Near(Time.timeScale, 0.08f, "Hit stop restores ongoing drawing slowdown");
        drawing.EndDrawing();
        Near(Time.timeScale, 0.2f, "Finishing drawing preserves ongoing extraction slowdown");
        yield return WaitReal(3.1f);
        Near(Time.timeScale, 5, "All time effect owners release without stuck slowdown");
        Load(KarmaKeyword.Light);
        drawing.BeginDrawing();
        storage.TryArmFromSlot(0, Vector3.zero);
        yield return WaitReal(3.15f);
        Near(Time.timeScale, 0.08f, "Extraction expiring during drawing keeps drawing slowdown");
        drawing.EndDrawing();
        Near(Time.timeScale, 5, "Ending drawing after extraction restores original scale");
        Near(Time.fixedDeltaTime, originalFixedDeltaTime, "Nested time effects restore original physics timestep");
        Load(KarmaKeyword.Curse);
        storage.TryArmFromSlot(0, Vector3.zero);
        drawing.BeginDrawing(true);
        storage.TryConsume(out _, out _);
        Near(Time.timeScale, 0.03f, "Consuming karma during right drawing preserves only drawing slowdown");
        drawing.EndDrawing();
        Near(Time.timeScale, 5, "Releasing drawing after karma consumption has no residual slowdown");
        Near(Time.fixedDeltaTime, originalFixedDeltaTime, "Karma consumption and drawing release restore physics timestep");
        Load(KarmaKeyword.Poison);
        storage.TryArmFromSlot(0, Vector3.zero);
        HitStop.Instance.Play(0.075f);
        storage.TryConsume(out _, out _);
        Near(Time.timeScale, 0, "Karma consumption preserves active hit stop");
        yield return WaitReal(0.15f);
        Near(Time.timeScale, 5, "Hit stop after karma consumption cannot restore a stale extraction slowdown");
        ClearBeads(); Load(KarmaKeyword.Light);
        Vector2 slotPoint = hud.GetSlotScreenRect(0).center;
        Pointer(hud, slotPoint, true); Pointer(hud, slotPoint, false);
        Check(!storage.HasArmedKeyword && !storage.Beads[0].IsEmpty, "Dropping back on HUD cancels without consuming bead");
        Pointer(hud, slotPoint, true); Pointer(hud, new Vector2(-10, -10), false);
        Check(!storage.HasArmedKeyword && !storage.Beads[0].IsEmpty, "Drop outside game viewport cancels safely");
        Pointer(hud, slotPoint, true); storage.Beads[0].remaining = 0;
        Pointer(hud, fieldPoint, false);
        Check(!storage.HasArmedKeyword && storage.Beads[0].IsRotten, "Bead expiring during drag cannot be armed");
        Pointer(hud, slotPoint, true);
        Check(!hud.IsDragging, "Rotten bead cannot begin drag");
        Pointer(hud, slotPoint, false);
        ClearBeads();
        Pointer(hud, slotPoint, true);
        Check(!hud.IsDragging && !(bool)Get(player.GetComponent<StringDrawer>(), "isDrawing"), "Empty bead click does not drag or draw");
        Pointer(hud, slotPoint, false);
        InputSystem.RemoveDevice(virtualMouse); virtualMouse = null;

        // String contact slows a real Rigidbody2D and restores it after leaving the string.
        center = playerBody.position + Vector2.right * 1.5f;
        var slowed = Bullet(KarmaKeyword.Poison, center);
        slowed.GetComponent<Rigidbody2D>().linearVelocity = Vector2.right * 4;
        capture.SlowAlongString(new List<Vector3> { center - Vector2.up, center + Vector2.up });
        Near(slowed.GetComponent<Rigidbody2D>().linearVelocity.x, 1, "String contact slows bullet to 25 percent");
        yield return Wait(0.5f);
        Near(slowed.GetComponent<Rigidbody2D>().linearVelocity.x, 4, "Bullet velocity recovers after slowdown");
        UnityEngine.Object.Destroy(slowed.gameObject);
        yield return CombatInputChecks();
        yield return OpenShieldChecks();
        yield return MovementEffectChecks();
        yield return DashChecks();

        // Damage and all status lifecycles on the real enemy.
        Enemy enemy = spawner.CurrentEnemy;
        float startingHealth = enemy.Health;
        enemy.ApplyKeyword(KarmaKeyword.Poison, 2, 1, player);
        yield return Wait(5.2f);
        Near(enemy.Health, startingHealth - 0.1f, "Poison deals exactly five percent per strength over five seconds");
        float afterPoison = enemy.Health;
        yield return Wait(0.5f);
        Near(enemy.Health, afterPoison, "Expired poison stops damaging");
        for (int i = 0; i < 5; i++) enemy.ApplyKeyword(KarmaKeyword.Curse, 8, 1, player);
        Check(enemy.CurseStacks == 30, "Curse caps at thirty stacks");
        startingHealth = enemy.Health;
        enemy.TakeDamage(1);
        Near(enemy.Health, startingHealth - 1.3f, "Curse amplifies ordinary damage by stack count");
        enemy.ClearCurse(); Check(enemy.CurseStacks == 0, "Curse removal clears stacks");
        yield return FreshEnemy(); enemy = spawner.CurrentEnemy;
        player.TakeDamage(50);
        enemy.ApplyKeyword(KarmaKeyword.Light, 2, 1, player);
        enemy.ApplyKeyword(KarmaKeyword.Light, 2, 1, player);
        Near(player.Health, player.MaxHealth - 50, "Same polarity refresh does not trigger removal");
        enemy.ApplyKeyword(KarmaKeyword.Dark, 2, 1, player);
        Near(player.Health, player.MaxHealth - 45, "Light-to-dark heals missing health using strength");
        startingHealth = enemy.Health;
        enemy.ApplyKeyword(KarmaKeyword.Light, 1, 1, player);
        Near(enemy.Health, startingHealth - 0.4f, "Dark-to-light causes removal burst");
        Near(enemy.OutgoingDamageMultiplier, 0.9f, "Dark removal weakens outgoing damage");
        float hp = player.Health;
        yield return Wait(6.2f);
        Near(player.Health, hp + (player.MaxHealth - hp) * 0.05f, "Light expires after six seconds and heals");
        enemy.ApplyKeyword(KarmaKeyword.Dark, 1, 1, player);
        startingHealth = enemy.Health;
        yield return Wait(6.2f);
        Near(enemy.Health, startingHealth - 0.2f, "Dark expires with burst damage");
        for (int i = 0; i < 4; i++)
        {
            enemy.ApplyKeyword(KarmaKeyword.Dark, 1, 0, player);
            enemy.ApplyKeyword(KarmaKeyword.Light, 1, 0, player);
        }
        Near(enemy.OutgoingDamageMultiplier, 0.7f, "Dark weakness caps at three stacks");
        yield return FreshEnemy(); enemy = spawner.CurrentEnemy;

        // A real CircleCast path hits once and delivers the selected keyword.
        ClearBeads(); Load(KarmaKeyword.Curse, 2);
        Check(storage.TryArmFromSlot(0, spawner.CurrentEnemy.transform.position), "Drop arms curse before path attack");
        Vector2 enemyPosition = enemy.transform.position;
        playerBody.position = enemyPosition + Vector2.left * 2;
        Physics2D.SyncTransforms();
        player.GetComponent<KnotAttack>().FollowDrawnPath(new List<Vector3> { playerBody.position, enemyPosition, enemyPosition + Vector2.right * 2 });
        yield return Wait(0.8f);
        Near(enemy.Health, 2, "Path physics damages enemy once despite multiple segments");
        Check(enemy.CurseStacks == 2 && storage.Beads[0].IsEmpty, "Path applies selected keyword and consumes bead");
        Check(!player.GetComponent<KnotAttack>().IsFollowingPath && !player.GetComponent<PlayerMovement>().MovementLocked, "Path completion releases player movement");
        Check(player.GetComponent<StringDrawer>().lineRenderer.positionCount == 0, "Path completion clears drawn line");
        playerBody.position = new Vector2(-8, 5); playerBody.linearVelocity = Vector2.zero;

        // Real projectile trigger/collision contact and weakened firing.
        enemy.ApplyKeyword(KarmaKeyword.Dark, 1, 0, player);
        enemy.ApplyKeyword(KarmaKeyword.Light, 1, 0, player);
        var thrower = enemy.GetComponent<EnemyProjectileThrower>();
        var existingBullets = new HashSet<CapturableProjectile>(UnityEngine.Object.FindObjectsByType<CapturableProjectile>(FindObjectsSortMode.None));
        Call(thrower, "ThrowProjectile");
        var emitted = Array.FindAll(UnityEngine.Object.FindObjectsByType<CapturableProjectile>(FindObjectsSortMode.None), bullet => !existingBullets.Contains(bullet));
        Check(emitted.Length == 4, "Enemy emits exactly four bullets in a single volley");
        HashSet<Vector2> velocities = new HashSet<Vector2>();
        foreach (var bullet in emitted)
        {
            velocities.Add(bullet.GetComponent<Rigidbody2D>().linearVelocity);
            Check(bullet.Keyword == KarmaKeyword.Poison, "All four volley bullets share one keyword");
        }
        MaterialPropertyBlock bulletColor = new MaterialPropertyBlock();
        emitted[0].GetComponent<Renderer>().GetPropertyBlock(bulletColor);
        Check(bulletColor.GetColor("_Color") == KarmaBeadHud.KeywordColor(KarmaKeyword.Poison),
            "Actual mesh projectile renderer displays its keyword color");
        Check(velocities.Count == 4, "Four simultaneous bullets have distinct fan directions");
        Near((float)Get(emitted[emitted.Length - 1], "sourceDamageMultiplier"), 0.9f, "New projectiles inherit dark weakness");
        Call(thrower, "ThrowProjectile");
        emitted = Array.FindAll(UnityEngine.Object.FindObjectsByType<CapturableProjectile>(FindObjectsSortMode.None), bullet => !existingBullets.Contains(bullet));
        Check(emitted.Length == 8 && Array.FindAll(emitted, bullet => bullet.Keyword == KarmaKeyword.Curse).Length == 4,
            "Second volley cycles all four bullets to next keyword");
        Call(thrower, "ThrowProjectile");
        emitted = Array.FindAll(UnityEngine.Object.FindObjectsByType<CapturableProjectile>(FindObjectsSortMode.None), bullet => !existingBullets.Contains(bullet));
        Check(Array.FindAll(emitted, bullet => !bullet.IsAbsorbable).Length == 1,
            "Every third volley contains one unabsorbable red-ring projectile");
        foreach (var bullet in UnityEngine.Object.FindObjectsByType<CapturableProjectile>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(bullet.gameObject);
        yield return null;
        var contact = UnityEngine.Object.Instantiate(projectilePrefab, player.transform.position + Vector3.right, Quaternion.identity);
        contact.GetComponent<Rigidbody2D>().gravityScale = 0;
        contact.Launch(Vector2.left * 4);
        hp = player.Health;
        yield return Wait(0.5f);
        Near(player.Health, hp - 10, "Projectile contact deals damage once");

        // Outbreak retains normal beads, empties only rotten beads, cannot be captured, tracks and hits once.
        ClearBeads(); Load(KarmaKeyword.Poison); Load(KarmaKeyword.Curse); Load(KarmaKeyword.Light); Load(KarmaKeyword.Dark);
        storage.Beads[0].remaining = storage.Beads[1].remaining = 0;
        yield return null; yield return null;
        Check(UnityEngine.Object.FindObjectsByType<KarmaOutbreak>(FindObjectsSortMode.None).Length == 0 && !storage.OutbreakWarningActive, "Two rotten beads do not trigger outbreak");
        storage.Beads[2].remaining = 0;
        yield return null; yield return null;
        Check(storage.OutbreakWarningActive && UnityEngine.Object.FindFirstObjectByType<KarmaOutbreak>() == null,
            "Three rotten beads warn before creating an outbreak");
        Near(Time.timeScale, 0.35f, "Outbreak warning grants slow-motion reaction window");
        Check(storage.TryArmFromSlot(0, player.transform.position), "Warning temporarily allows using a rotten ability");
        storage.TryConsume(out _, out _);
        yield return null; yield return null;
        Check(!storage.OutbreakWarningActive, "Using a warned bead cancels outbreak below threshold");
        yield return WaitReal(3.15f);
        Load(KarmaKeyword.Poison); storage.Beads[0].remaining = 0;
        Time.timeScale = 1f;
        yield return null; yield return null;
        Check(storage.OutbreakWarningActive, "Ignoring new rotten accumulation warns again");
        Check(storage.TryArmFromSlot(0, player.transform.position) && !storage.Beads[0].IsEmpty,
            "Last-chance rotten extraction keeps its slot until an attack actually consumes it");
        yield return WaitReal(2.15f);
        var orb = UnityEngine.Object.FindFirstObjectByType<KarmaOutbreak>();
        Check(orb != null && storage.Beads[0].IsEmpty && storage.Beads[1].IsEmpty && storage.Beads[2].IsEmpty
            && !storage.Beads[3].IsEmpty, "Three rotten beads produce one orb and preserve healthy bead");
        Check(!storage.HasArmedKeyword, "Discarding a warned ability without attacking does not prevent outbreak");
        Check(orb.GetComponent<CapturableProjectile>() == null, "Outbreak cannot be captured as a projectile");
        var orbLoop = Loop(orb.transform.position);
        capture.TryCaptureClosedShape(orbLoop, out count);
        Check(count == 0 && orb != null, "Closed string cannot absorb outbreak");
        Check(orb.IsTelegraphing, "Outbreak visibly telegraphs before launching");
        Vector2 startOrb = orb.transform.position;
        hp = player.Health;
        yield return WaitReal(0.55f);
        Check(Vector2.Distance(orb.transform.position, player.transform.position) < Vector2.Distance(startOrb, player.transform.position), "Outbreak moves toward player");
        yield return Wait(1.4f);
        Near(player.Health, Mathf.Max(0, hp - 45), "Three-bead outbreak deals 45 damage once");
        Check(UnityEngine.Object.FindFirstObjectByType<KarmaOutbreak>() == null, "Outbreak disappears on contact");
        Check(!player.IsDead, "Player remains alive for remaining checks");
        foreach (Enemy activeEnemy in spawner.AliveEnemies) Call(activeEnemy, "RemovePolarity");
        Vector2 dodgeOrigin = playerBody.position;
        hp = player.Health;
        var avoidable = KarmaOutbreak.Spawn(player, 2f, 45f, 1f);
        playerBody.position = dodgeOrigin + Vector2.right * 8f;
        playerBody.linearVelocity = Vector2.zero;
        yield return WaitReal(0.65f);
        yield return Wait(1.15f);
        Check(avoidable == null || avoidable.HasExploded, "Outbreak detonates after its limited flight time even without contact");
        Near(player.Health, hp, "Moving outside explosion radius avoids outbreak damage");
        yield return WaitReal(0.3f);
        playerBody.position = dodgeOrigin; playerBody.linearVelocity = Vector2.zero;

        ClearBeads();
        for (int i = 0; i < 5; i++)
        {
            yield return FreshEnemy();
            Check(spawner.AliveCount == 2, "Respawn cycle " + (i + 1) + " maintains two enemies without over-spawning");
            Check(spawner.CurrentEnemy.Health == 3 && spawner.CurrentEnemy.CurseStacks == 0
                && Mathf.Approximately(spawner.CurrentEnemy.OutgoingDamageMultiplier, 1), "Respawn resets health and statuses");
            Check((Transform)Get(spawner.CurrentEnemy.GetComponent<EnemyProjectileThrower>(), "target") == expectedTarget, "Respawn retains original player target");
        }
        Time.timeScale = 1;
        // Keep the real movement/jump components enabled to catch execution-order conflicts.
        ClearBeads();
        enemy = spawner.CurrentEnemy;
        player.GetComponent<PlayerMovement>().enabled = true;
        player.GetComponent<PlayerJump>().enabled = true;
        playerBody.position = (Vector2)enemy.transform.position + Vector2.left * 3;
        playerBody.linearVelocity = Vector2.zero;
        player.GetComponent<KnotAttack>().FollowDrawnPath(new List<Vector3> {
            playerBody.position, enemy.transform.position, enemy.transform.position + Vector3.right * 3 });
        yield return Wait(0.12f);
        Check(player.GetComponent<KnotAttack>().IsFollowingPath, "Live movement path is still running during mid-path check");
        Near(playerBody.gravityScale, 0, "Jump component preserves zero gravity during path attack");
        yield return Wait(0.6f);
        Check(!player.GetComponent<KnotAttack>().IsFollowingPath && !player.GetComponent<PlayerMovement>().MovementLocked,
            "Path completes with actual movement and jump enabled");
        player.GetComponent<PlayerMovement>().enabled = false;
        player.GetComponent<PlayerJump>().enabled = false;
        playerBody.gravityScale = 0;
        playerBody.position = new Vector2(-2, 1); playerBody.linearVelocity = Vector2.zero;
        foreach (KarmaKeyword kind in Enum.GetValues(typeof(KarmaKeyword))) Load(kind);
        storage.TryArmFromSlot(0, (Vector2)player.transform.position + Vector2.right * 2 + Vector2.up);
        foreach (Enemy activeEnemy in spawner.AliveEnemies) Call(activeEnemy.GetComponent<EnemyProjectileThrower>(), "ThrowProjectile");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/karma-play-preview.png"));
        yield return Wait(1f);
        yield return EndlessGroundChecks();

        // Death prevents actions and further spawns.
        storage.TryArmFromSlot(1, player.transform.position);
        player.TakeDamage(10000);
        yield return null; yield return null;
        Check(!storage.IsExtractionActive && !storage.HasArmedKeyword, "Player death cancels extraction and tint");
        Near(Time.timeScale, 1, "Player death releases extraction slow motion");
        Check(player.IsDead && player.GetComponent<PlayerMovement>().MovementLocked && !player.GetComponent<StringDrawer>().enabled, "Death locks movement and drawing");
        foreach (Enemy activeEnemy in spawner.AliveEnemies) activeEnemy.TakeDamage(10000);
        yield return Wait(1.1f);
        Check(spawner.AliveCount == 0, "All enemy spawning stops after player death");
        yield return RestartChecks();
        Check(errors.Count == 0, "No runtime error/exception/assert logs during suite");
    }

    [Serializable] private class Report
    {
        public bool success;
        public int checks;
        public string[] passed, errors, editorDiagnostics;
    }
    private static void Finish()
    {
        routines.Clear();
        Application.logMessageReceived -= OnLog;
        if (virtualKeyboard != null) InputSystem.RemoveDevice(virtualKeyboard);
        if (virtualMouse != null) InputSystem.RemoveDevice(virtualMouse);
        if (storage != null) storage.enabled = false;
        if (player != null) player.GetComponent<DrawingModeController>()?.EndDrawing();
        if (HitStop.Instance != null) HitStop.Instance.enabled = false;
        Time.timeScale = originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;
        InputSystem.settings.editorInputBehaviorInPlayMode = originalInputBehavior;
        InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/karma-play-results.json", JsonUtility.ToJson(new Report {
            success = errors.Count == 0, checks = checks, passed = passed.ToArray(), errors = errors.ToArray(), editorDiagnostics = editorDiagnostics.ToArray()
        }, true));
        Debug.Log($"Karma Play validation: {passed.Count}/{checks} checks passed, {errors.Count} errors.");
        EditorApplication.isPlaying = false;
    }
}
#endif
