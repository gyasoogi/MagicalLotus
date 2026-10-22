using System.Collections;
using UnityEngine;

public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }

    private Coroutine routine;

    private void Awake()
    {
        Instance = this;
    }

    public void Play(float duration)
    {
        if (routine != null) StopCoroutine(routine);

        routine = StartCoroutine(StopForSeconds(duration));
    }

    private IEnumerator StopForSeconds(float duration)
    {
        GameTimeEffects.Request(this, 0f);
        yield return new WaitForSecondsRealtime(duration);
        GameTimeEffects.Release(this);
        routine = null;
    }

    private void OnDestroy()
    {
        GameTimeEffects.Release(this);
        if (Instance == this) Instance = null;
    }

    private void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        GameTimeEffects.Release(this);
    }
}
