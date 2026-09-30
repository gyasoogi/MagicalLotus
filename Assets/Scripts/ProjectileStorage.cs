using UnityEngine;

public class ProjectileStorage : MonoBehaviour
{
    [SerializeField] private int storedProjectileCount;

    public int StoredProjectileCount => storedProjectileCount;

    public void Store(CapturableProjectile projectile)
    {
        if (projectile == null) return;

        storedProjectileCount++;
        projectile.Capture();
        Debug.Log("Stored projectile: " + storedProjectileCount);
    }
}
