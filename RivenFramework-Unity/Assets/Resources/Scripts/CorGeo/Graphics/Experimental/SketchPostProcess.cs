using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class SketchPostProcess : MonoBehaviour
{
    public Material effectMaterial;

    [Range(0.0f, 5.0f)]
    public float edgeStrength = 1.5f;

    [Range(2, 1000)]
    public int posterizeLevels = 4;

    public Texture2D hatchTexture;
    [Range(0.0f, 2.0f)]
    public float hatchStrength = 0.5f;
    [Range(0.1f, 50f)]
    public float hatchScale = 4f;


    private Camera cam;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        // Enables both depth and normal textures from the pipeline
        cam.depthTextureMode |= DepthTextureMode.DepthNormals;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (effectMaterial == null)
        {
            Graphics.Blit(src, dst);
            return;
        }

        effectMaterial.SetFloat("_EdgeStrength", edgeStrength);
        effectMaterial.SetInt("_PosterizeLevels", posterizeLevels);
        effectMaterial.SetFloat("_HatchStrength", hatchStrength);
        effectMaterial.SetFloat("_HatchScale", hatchScale);

        if (hatchTexture != null)
            effectMaterial.SetTexture("_HatchTex", hatchTexture);

        Graphics.Blit(src, dst, effectMaterial);
    }
}