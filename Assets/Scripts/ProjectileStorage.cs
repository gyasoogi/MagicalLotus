using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public class ProjectileStorage : MonoBehaviour
{
    [SerializeField, Min(1f)] private float decaySeconds = 20f;
    [SerializeField, Range(1, 8)] private int outbreakThreshold = 3;
    [SerializeField, Min(0.1f)] private float outbreakSpeed = 2f;
    [SerializeField, Min(0f)] private float outbreakDamage = 15f;
    [SerializeField, Min(1f)] private float outbreakLifetime = 20f;
    [SerializeField, Min(0f)] private float outbreakWarningSeconds = 2f;
    [SerializeField, Range(0.01f, 1f)] private float warningTimeScale = 0.35f;
    [SerializeField, Min(1f)] private float maxEnergy = 100f;
    [SerializeField, Min(0.1f)] private float energyPerKarma = 3f;
    private float energy;
    private float warningEnds;
    private readonly object warningOwner = new object();
    public float Energy => energy;
    public float EnergyFraction => energy / Mathf.Max(1f, maxEnergy);
    public bool OutbreakWarningActive => warningEnds > 0f;
    public float OutbreakWarningRemaining => Mathf.Max(0f, warningEnds - Time.unscaledTime);
    public bool CanExtract(KarmaBead bead) => !bead.IsEmpty && (!bead.IsRotten || OutbreakWarningActive);
    public void AddEnergy(float amount) { energy = Mathf.Clamp(energy + Mathf.Max(0f, amount), 0f, maxEnergy); }
    [SerializeField] private KarmaBead[] beads = new KarmaBead[8];
    [SerializeField, Min(0.1f)] private float extractionDuration = 3f;
    [SerializeField, Range(0.01f, 1f)] private float extractionTimeScale = 0.2f;
    private KarmaKeyword armedKeyword;
    private int armedPower;
    private Vector3 armedPosition;
    private float extractionEnds;
    private int warningRescueSlot = -1;
    public bool IsExtractionActive => extractionEnds > Time.unscaledTime;
    public float ExtractionRemaining => Mathf.Max(0f, extractionEnds - Time.unscaledTime);
    public bool HasArmedKeyword => armedPower > 0 && IsExtractionActive;
    public KarmaKeyword ArmedKeyword => armedKeyword;
    public int ArmedPower => armedPower;
    public Vector3 ArmedPosition => armedPosition;
    public IReadOnlyList<KarmaBead> Beads => beads;
    public int StoredProjectileCount
    {
        get { int count = 0; foreach (var bead in beads) count += bead.power; return count; }
    }

    private void Awake()
    {
        if (GetComponent<PlayerHealth>() == null) gameObject.AddComponent<PlayerHealth>();
        if (beads == null || beads.Length != 8) beads = new KarmaBead[8];
        for (int i = 0; i < beads.Length; i++)
            if (beads[i] == null) beads[i] = new KarmaBead();
        if (GetComponent<KarmaBeadHud>() == null) gameObject.AddComponent<KarmaBeadHud>();
        if (GetComponent<EndlessGround>() == null) gameObject.AddComponent<EndlessGround>();
    }

    private void Update()
    {
        if (extractionEnds > 0f && (!IsExtractionActive || GetComponent<PlayerHealth>().IsDead)) EndExtraction();
        if (GetComponent<PlayerHealth>().IsDead) { CancelWarning(); return; }
        int rotten = 0;
        foreach (KarmaBead bead in beads)
        {
            if (bead.IsEmpty) continue;
            bead.remaining = Mathf.Max(0f, bead.remaining - Time.deltaTime);
            if (bead.IsRotten) rotten++;
        }
        if (rotten < Mathf.Clamp(outbreakThreshold, 1, beads.Length)) { CancelWarning(); return; }
        if (!OutbreakWarningActive)
        {
            warningEnds = Time.unscaledTime + Mathf.Max(0f, outbreakWarningSeconds);
            GameTimeEffects.Request(warningOwner, warningTimeScale);
            return;
        }
        if (Time.unscaledTime < warningEnds) return;
        if (warningRescueSlot >= 0) EndExtraction();
        CancelWarning();
        foreach (KarmaBead bead in beads)
            if (bead.IsRotten) bead.Clear();
        KarmaOutbreak.Spawn(GetComponent<PlayerHealth>(), outbreakSpeed,
            outbreakDamage * rotten, outbreakLifetime);
    }

    private void CancelWarning() { warningEnds = 0f; GameTimeEffects.Release(warningOwner); }

    private void EndExtraction()
    {
        armedPower = 0;
        extractionEnds = 0f;
        warningRescueSlot = -1;
        GameTimeEffects.Release(this);
    }

    private void OnDisable() { EndExtraction(); CancelWarning(); }

    // Fresh matching beads merge across captures; rotten beads keep occupying their slots.
    public int StoreBatch(List<CapturableProjectile> projectiles)
    {
        int captured = 0;
        if (projectiles == null) return captured;
        HashSet<CapturableProjectile> available = new HashSet<CapturableProjectile>(projectiles);
        for (int kind = 0; kind < 4; kind++)
        {
            KarmaBead destination = System.Array.Find(beads, bead => !bead.IsEmpty && !bead.IsRotten && (int)bead.keyword == kind)
                ?? System.Array.Find(beads, bead => bead.IsEmpty);
            if (destination == null || destination.power >= 8) continue;
            int count = 0;
            foreach (var projectile in available)
            {
                if (destination.power + count >= 8 || energy < energyPerKarma) break;
                if (projectile == null || !projectile.CanCapture || (int)projectile.Keyword != kind) continue;
                if (!projectile.TryClaimKarma()) continue;
                energy -= energyPerKarma;
                count++;
                captured++;
            }
            if (count == 0) continue;
            destination.keyword = (KarmaKeyword)kind;
            destination.power = Mathf.Min(8, destination.power + count);
            destination.lifetime = destination.remaining = Mathf.Max(1f, decaySeconds);
        }
        return captured;
    }

    public void Store(CapturableProjectile projectile)
    {
        if (projectile != null) StoreBatch(new List<CapturableProjectile> { projectile });
    }

    public bool TryConsume(out KarmaKeyword keyword, out int power)
    {
        keyword = armedKeyword;
        power = 0;
        if (!HasArmedKeyword || GetComponent<PlayerHealth>().IsDead) return false;
        power = armedPower;
        if (warningRescueSlot >= 0)
        {
            beads[warningRescueSlot].Clear();
            warningRescueSlot = -1;
        }
        EndExtraction();
        return true;
    }

    // Committing an attack ends preparation slow motion; the keyword is still
    // consumed only after the player has physically moved far enough to attack.
    public void ReleaseAttackPreparationSlowMotion()
    {
        if (HasArmedKeyword) GameTimeEffects.Release(this);
    }

    // The three-second window uses real time, independently of slow motion and hit stop.
    public bool TryArmFromSlot(int index, Vector3 fieldPosition)
    {
        if (HasArmedKeyword || GetComponent<PlayerHealth>().IsDead || index < 0 || index >= beads.Length) return false;
        if (float.IsNaN(fieldPosition.x) || float.IsNaN(fieldPosition.y)
            || float.IsInfinity(fieldPosition.x) || float.IsInfinity(fieldPosition.y)) return false;
        KarmaBead bead = beads[index];
        if (!CanExtract(bead)) return false;
        armedKeyword = bead.keyword;
        armedPower = bead.power;
        armedPosition = new Vector3(fieldPosition.x, fieldPosition.y, 0f);
        warningRescueSlot = bead.IsRotten ? index : -1;
        extractionEnds = Time.unscaledTime + Mathf.Max(0.1f, extractionDuration);
        GameTimeEffects.Request(this, extractionTimeScale);
        if (warningRescueSlot < 0) bead.Clear();
        return true;
    }
}
