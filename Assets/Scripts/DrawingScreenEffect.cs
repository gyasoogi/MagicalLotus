using UnityEngine;

[RequireComponent(typeof(Camera))]
public class DrawingScreenEffect : MonoBehaviour
{
    [SerializeField] private Material grayscaleMaterial;

    private bool effectActive;

    public void SetActive(bool active)
    {
        effectActive = active;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (effectActive && grayscaleMaterial != null)
            Graphics.Blit(source, destination, grayscaleMaterial);
        else
            Graphics.Blit(source, destination);
    }
}
