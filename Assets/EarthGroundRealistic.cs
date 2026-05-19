using UnityEngine;

/// <summary>
/// Generates a realistic procedural ground with vegetation, detailed textures, and multi-layer detail.
/// Replaces or enhances EarthGroundVisual with more advanced procedural generation.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class EarthGroundRealistic : MonoBehaviour
{
    [Header("Texture Sizes")]
    public int albedoSize = 1024;
    public int normalSize = 1024;
    public int roughnessSize = 512;

    [Header("Base Colors")]
    public Color grassColor = new Color(0.15f, 0.38f, 0.2f, 1f);
    public Color dryGrassColor = new Color(0.45f, 0.4f, 0.2f, 1f);
    public Color soilColor = new Color(0.28f, 0.19f, 0.11f, 1f);
    public Color stoneColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("Vegetation")]
    public bool enableVegetation = true;
    public Color vegetationColor = new Color(0.1f, 0.25f, 0.08f, 1f);
    public float vegetationAmount = 0.3f; // 0-1
    public float vegetationThreshold = 0.55f; // Noise value above which vegetation appears

    [Header("Material Properties")]
    public float baseTiling = 18f;
    public float detailTiling = 3.2f;
    public float normalStrength = 0.5f;
    public float roughnessBase = 0.75f;
    public float roughnessVariation = 0.2f;

    [Header("Noise Parameters")]
    public float largeScale = 5.5f;
    public float mediumScale = 22f;
    public float fineScale = 95f;
    public float vegetationScale = 15f;

    private Material generatedMaterial;

    void Start()
    {
        ApplyRealisticGroundMaterial();
    }

    void ApplyRealisticGroundMaterial()
    {
        Renderer groundRenderer = GetComponent<Renderer>();
        Shader shader = Shader.Find("Standard");

        generatedMaterial = new Material(shader != null ? shader : groundRenderer.sharedMaterial.shader);
        generatedMaterial.name = "Procedural Earth Ground - Realistic";

        // Generate and apply textures
        generatedMaterial.mainTexture = GenerateAlbedoTexture();
        generatedMaterial.mainTextureScale = new Vector2(baseTiling, baseTiling);

        Texture2D normalMap = GenerateNormalMap();
        generatedMaterial.SetTexture("_BumpMap", normalMap);
        generatedMaterial.SetFloat("_BumpScale", normalStrength);
        generatedMaterial.EnableKeyword("_NORMALMAP");

        Texture2D roughnessMap = GenerateRoughnessMap();
        generatedMaterial.SetTexture("_OcclusionMap", roughnessMap);
        generatedMaterial.SetFloat("_OcclusionStrength", 1.0f);
        generatedMaterial.EnableKeyword("_OCCLUSION");

        // Material properties
        generatedMaterial.SetFloat("_Glossiness", 1f - roughnessBase);
        generatedMaterial.SetFloat("_Metallic", 0.02f); // Slight metallic from minerals

        groundRenderer.material = generatedMaterial;
    }

    Texture2D GenerateAlbedoTexture()
    {
        Texture2D texture = new Texture2D(albedoSize, albedoSize, TextureFormat.RGB24, true);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;

        for (int y = 0; y < albedoSize; y++)
        {
            for (int x = 0; x < albedoSize; x++)
            {
                float nx = (float)x / albedoSize;
                float ny = (float)y / albedoSize;

                // Multi-octave noise for natural variation
                float largeNoise = Mathf.PerlinNoise(nx * largeScale, ny * largeScale);
                float mediumNoise = Mathf.PerlinNoise(nx * mediumScale + 18.7f, ny * mediumScale + 9.3f);
                float fineNoise = Mathf.PerlinNoise(nx * fineScale + 2.1f, ny * fineScale + 41.2f);
                float vegetationNoise = enableVegetation ? Mathf.PerlinNoise(nx * vegetationScale + 100f, ny * vegetationScale + 50f) : 0f;

                // Base terrain color - grass, dry grass, soil
                Color terrainColor = Color.Lerp(soilColor, grassColor, largeNoise);
                terrainColor = Color.Lerp(terrainColor, dryGrassColor, Mathf.Clamp01(mediumNoise - 0.5f) * 0.85f);

                // Add stone patches
                float stoneNoise = Mathf.PerlinNoise(nx * 8f + 33f, ny * 8f + 77f);
                if (stoneNoise > 0.72f)
                {
                    float stoneMix = Mathf.Clamp01((stoneNoise - 0.72f) * 5f);
                    terrainColor = Color.Lerp(terrainColor, stoneColor, stoneMix * 0.4f);
                }

                // Vegetation overlay
                Color finalColor = terrainColor;
                if (enableVegetation && vegetationNoise > vegetationThreshold)
                {
                    float vegMix = Mathf.Clamp01((vegetationNoise - vegetationThreshold) / (1f - vegetationThreshold)) * vegetationAmount;
                    finalColor = Color.Lerp(terrainColor, vegetationColor, vegMix);
                }

                // Fine detail brightness variation
                float brightness = Mathf.Lerp(0.8f, 1.2f, fineNoise);
                finalColor = finalColor * brightness;

                // Add subtle color variation
                float colorVar = Mathf.PerlinNoise(nx * 200f, ny * 200f);
                Color variation = new Color(colorVar - 0.5f, colorVar - 0.5f, colorVar - 0.5f, 0f) * 0.05f;
                finalColor = finalColor + variation;

                texture.SetPixel(x, y, finalColor);
            }
        }

        texture.Apply(true);
        return texture;
    }

    Texture2D GenerateNormalMap()
    {
        Texture2D texture = new Texture2D(normalSize, normalSize, TextureFormat.RGBA32, true);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;

        for (int y = 0; y < normalSize; y++)
        {
            for (int x = 0; x < normalSize; x++)
            {
                float nx = (float)x / normalSize;
                float ny = (float)y / normalSize;

                // Multi-scale height for better details
                float largeHeight = Mathf.PerlinNoise(nx * 15f, ny * 15f);
                float mediumHeight = Mathf.PerlinNoise(nx * 45f + 100f, ny * 45f + 200f) * 0.5f;
                float fineHeight = Mathf.PerlinNoise(nx * 120f + 300f, ny * 120f + 400f) * 0.25f;

                float height = largeHeight + mediumHeight + fineHeight;

                // Calculate normal from height derivatives
                float heightRight = Mathf.PerlinNoise((nx + 1f / normalSize) * 15f, ny * 15f)
                    + Mathf.PerlinNoise((nx + 1f / normalSize) * 45f + 100f, ny * 45f + 200f) * 0.5f
                    + Mathf.PerlinNoise((nx + 1f / normalSize) * 120f + 300f, ny * 120f + 400f) * 0.25f;

                float heightUp = Mathf.PerlinNoise(nx * 15f, (ny + 1f / normalSize) * 15f)
                    + Mathf.PerlinNoise(nx * 45f + 100f, (ny + 1f / normalSize) * 45f + 200f) * 0.5f
                    + Mathf.PerlinNoise(nx * 120f + 300f, (ny + 1f / normalSize) * 120f + 400f) * 0.25f;

                Vector3 normal = new Vector3((height - heightRight) * 3f, (height - heightUp) * 3f, 1f).normalized;
                Color normalColor = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);

                texture.SetPixel(x, y, normalColor);
            }
        }

        texture.Apply(true);
        return texture;
    }

    Texture2D GenerateRoughnessMap()
    {
        Texture2D texture = new Texture2D(roughnessSize, roughnessSize, TextureFormat.R8, true);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;

        for (int y = 0; y < roughnessSize; y++)
        {
            for (int x = 0; x < roughnessSize; x++)
            {
                float nx = (float)x / roughnessSize;
                float ny = (float)y / roughnessSize;

                // Create roughness variation - grass is rougher, stones less rough
                float grassNoise = Mathf.PerlinNoise(nx * largeScale, ny * largeScale);
                float detailNoise = Mathf.PerlinNoise(nx * 100f, ny * 100f);

                float roughness = roughnessBase + (grassNoise - 0.5f) * roughnessVariation;
                roughness = Mathf.Clamp01(roughness + (detailNoise - 0.5f) * 0.1f);

                Color roughColor = new Color(roughness, 0f, 0f, 1f);
                texture.SetPixel(x, y, roughColor);
            }
        }

        texture.Apply(true);
        return texture;
    }

    public void RegenerateTextures()
    {
        if (generatedMaterial != null)
        {
            generatedMaterial.mainTexture = GenerateAlbedoTexture();
            generatedMaterial.SetTexture("_BumpMap", GenerateNormalMap());
            generatedMaterial.SetTexture("_OcclusionMap", GenerateRoughnessMap());
        }
    }
}
