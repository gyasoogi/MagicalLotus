using System.Collections;
using UnityEngine;

public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }

    private Coroutine routine;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        Instance = this;
    }

    public void Play(float duration)
    {
        if (routine == null) previousTimeScale = Time.timeScale;
        else StopCoroutine(routine);

        routine = StartCoroutine(StopForSeconds(duration));
    }

    private IEnumerator StopForSeconds(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = previousTimeScale;
        routine = null;
    }

    private void OnDestroy()
    {
        if (routine != null) Time.timeScale = previousTimeScale;
        if (Instance == this) Instance = null;
    }
}
