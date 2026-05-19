using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class EarthGroundVisual : MonoBehaviour
{
    public int textureSize = 512;
    public float textureTiling = 18f;
    public Color grassColor = new Color(0.12f, 0.32f, 0.18f, 1f);
    public Color soilColor = new Color(0.28f, 0.19f, 0.11f, 1f);
    public Color dryGrassColor = new Color(0.42f, 0.36f, 0.18f, 1f);
    public float bumpStrength = 0.35f;

    private Material generatedMaterial;

    void Start()
    {
        ApplyGroundMaterial();
    }

    void ApplyGroundMaterial()
    {
        Renderer groundRenderer = GetComponent<Renderer>();
        Shader shader = Shader.Find("Standard");

        generatedMaterial = new Material(shader != null ? shader : groundRenderer.sharedMaterial.shader);
        generatedMaterial.name = "Procedural Earth Ground";
        generatedMaterial.mainTexture = GenerateAlbedoTexture();
        generatedMaterial.mainTextureScale = new Vector2(textureTiling, textureTiling);
        generatedMaterial.SetFloat("_Glossiness", 0.12f);
        generatedMaterial.SetFloat("_Metallic", 0f);

        Texture2D normalMap = GenerateNormalTexture();
        generatedMaterial.SetTexture("_BumpMap", normalMap);
        generatedMaterial.SetFloat("_BumpScale", bumpStrength);
        generatedMaterial.EnableKeyword("_NORMALMAP");

        groundRenderer.material = generatedMaterial;
    }

    Texture2D GenerateAlbedoTexture()
    {
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float nx = (float)x / textureSize;
                float ny = (float)y / textureSize;

                float largeNoise = Mathf.PerlinNoise(nx * 5.5f, ny * 5.5f);
                float mediumNoise = Mathf.PerlinNoise(nx * 22f + 18.7f, ny * 22f + 9.3f);
                float fineNoise = Mathf.PerlinNoise(nx * 95f + 2.1f, ny * 95f + 41.2f);

                Color baseColor = Color.Lerp(soilColor, grassColor, largeNoise);
                baseColor = Color.Lerp(baseColor, dryGrassColor, Mathf.Clamp01(mediumNoise - 0.55f) * 0.9f);

                float brightness = Mathf.Lerp(0.78f, 1.22f, fineNoise);
                Color finalColor = baseColor * brightness;
                finalColor.a = 1f;

                texture.SetPixel(x, y, finalColor);
            }
        }

        texture.Apply(true);
        return texture;
    }

    Texture2D GenerateNormalTexture()
    {
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float nx = (float)x / textureSize;
                float ny = (float)y / textureSize;

                float height = Mathf.PerlinNoise(nx * 65f, ny * 65f);
                float heightRight = Mathf.PerlinNoise((nx + 1f / textureSize) * 65f, ny * 65f);
                float heightUp = Mathf.PerlinNoise(nx * 65f, (ny + 1f / textureSize) * 65f);

                Vector3 normal = new Vector3((height - heightRight) * 2f, (height - heightUp) * 2f, 1f).normalized;
                Color normalColor = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);

                texture.SetPixel(x, y, normalColor);
            }
        }

        texture.Apply(true);
        return texture;
    }
}
