using UnityEngine;

[RequireComponent(typeof(Camera))]
public class DrawingScreenEffect : MonoBehaviour
{
    [SerializeField] private Material grayscaleMaterial;

    private bool effectActive;
    private Material fallbackMaterial;
    public bool IsActive => effectActive;

    private void EnsureMaterial()
    {
        if (grayscaleMaterial != null && grayscaleMaterial.shader != null && grayscaleMaterial.shader.isSupported) return;
        if (fallbackMaterial == null)
        {
            Shader shader = Shader.Find("Hidden/MagicalLotus/DrawingGrayscale");
            if (shader != null && shader.isSupported)
                fallbackMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        grayscaleMaterial = fallbackMaterial;
    }

    public void SetActive(bool active)
    {
        effectActive = active;
        if (active) EnsureMaterial();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (effectActive && grayscaleMaterial != null)
            Graphics.Blit(source, destination, grayscaleMaterial);
        else
            Graphics.Blit(source, destination);
    }

    private void OnDisable() { effectActive = false; }
    private void OnDestroy()
    {
        if (fallbackMaterial != null) Destroy(fallbackMaterial);
    }
}
