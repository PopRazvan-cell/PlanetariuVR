using UnityEngine;

/// <summary>
/// Spawns small vegetation objects (grass tufts, bushes) procedurally on the ground.
/// Works with EarthGroundRealistic to create a fully detailed natural landscape.
/// </summary>
public class GroundVegetationSpawner : MonoBehaviour
{
    [Header("Vegetation Mesh Settings")]
    public bool spawnGrassTufts = true;
    public bool spawnBushes = true;
    
    [Header("Grass Tufts")]
    public int grassTuftsCount = 1500;
    public float grassHeightMin = 0.1f;
    public float grassHeightMax = 0.35f;
    public float grassScaleMin = 0.15f;
    public float grassScaleMax = 0.4f;
    public Material grassMaterial;

    [Header("Bushes")]
    public int bushCount = 250;
    public float bushHeightMin = 0.5f;
    public float bushHeightMax = 1.2f;
    public float bushScaleMin = 0.3f;
    public float bushScaleMax = 0.8f;
    public Material bushMaterial;

    [Header("Distribution")]
    public float spawnRadius = 100f;
    public float noiseScale = 15f;
    public float densityThreshold = 0.45f; // Perlin noise value above which vegetation spawns

    [Header("Optimization")]
    public bool useMeshBatching = true;
    public int batchSize = 100;

    private GameObject grassContainer;
    private GameObject bushContainer;
    private Mesh grassMesh;
    private Mesh bushMesh;

    void Start()
    {
        InitializeVegetation();
    }

    void InitializeVegetation()
    {
        // Create containers for organization
        grassContainer = new GameObject("GrassTufts");
        grassContainer.transform.SetParent(transform);
        grassContainer.transform.localPosition = Vector3.zero;

        bushContainer = new GameObject("Bushes");
        bushContainer.transform.SetParent(transform);
        bushContainer.transform.localPosition = Vector3.zero;

        // Create simple meshes
        grassMesh = CreateGrassMesh();
        bushMesh = CreateBushMesh();

        // Spawn vegetation
        if (spawnGrassTufts)
            SpawnGrassTufts();
        if (spawnBushes)
            SpawnBushes();
    }

    Mesh CreateGrassMesh()
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[8];
        int[] triangles = new int[12];

        // Create a simple quad-based grass tuft
        float halfWidth = 0.05f;

        vertices[0] = new Vector3(-halfWidth, 0, 0);
        vertices[1] = new Vector3(halfWidth, 0, 0);
        vertices[2] = new Vector3(-halfWidth, 1, 0);
        vertices[3] = new Vector3(halfWidth, 1, 0);

        vertices[4] = new Vector3(0, 0, -halfWidth);
        vertices[5] = new Vector3(0, 0, halfWidth);
        vertices[6] = new Vector3(0, 1, -halfWidth);
        vertices[7] = new Vector3(0, 1, halfWidth);

        // Front quad
        triangles[0] = 0; triangles[1] = 2; triangles[2] = 1;
        triangles[3] = 1; triangles[4] = 2; triangles[5] = 3;

        // Side quad
        triangles[6] = 4; triangles[7] = 6; triangles[8] = 5;
        triangles[9] = 5; triangles[10] = 6; triangles[11] = 7;

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    Mesh CreateBushMesh()
    {
        Mesh mesh = new Mesh();
        
        // Create a simple sphere-like bush
        int segments = 4;
        Vector3[] vertices = new Vector3[(segments + 1) * (segments + 1)];
        
        for (int i = 0; i <= segments; i++)
        {
            for (int j = 0; j <= segments; j++)
            {
                float phi = Mathf.PI * i / segments;
                float theta = 2 * Mathf.PI * j / segments;
                
                float x = 0.3f * Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = 0.3f * Mathf.Cos(phi) + 0.3f;
                float z = 0.3f * Mathf.Sin(phi) * Mathf.Sin(theta);
                
                vertices[i * (segments + 1) + j] = new Vector3(x, y, z);
            }
        }

        int[] triangles = new int[segments * segments * 6];
        int triIndex = 0;

        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                int a = i * (segments + 1) + j;
                int b = a + 1;
                int c = a + (segments + 1);
                int d = c + 1;

                triangles[triIndex++] = a;
                triangles[triIndex++] = c;
                triangles[triIndex++] = b;
                
                triangles[triIndex++] = b;
                triangles[triIndex++] = c;
                triangles[triIndex++] = d;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    void SpawnGrassTufts()
    {
        int spawned = 0;
        int attempts = 0;
        int maxAttempts = grassTuftsCount * 3;

        while (spawned < grassTuftsCount && attempts < maxAttempts)
        {
            attempts++;
            
            float angle = Random.Range(0f, 360f);
            float distance = Random.Range(0f, spawnRadius);
            
            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * distance;
            float z = Mathf.Sin(angle * Mathf.Deg2Rad) * distance;
            
            // Use noise to determine if grass should spawn
            float noiseVal = Mathf.PerlinNoise((x / spawnRadius + 1f) * 0.5f * noiseScale, 
                                              (z / spawnRadius + 1f) * 0.5f * noiseScale);
            
            if (noiseVal > densityThreshold)
            {
                Vector3 position = new Vector3(x, 0, z);
                Quaternion rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                float scale = Random.Range(grassScaleMin, grassScaleMax);
                float height = Random.Range(grassHeightMin, grassHeightMax);

                GameObject grass = new GameObject($"Grass_{spawned}");
                grass.transform.SetParent(grassContainer.transform);
                grass.transform.localPosition = position;
                grass.transform.localRotation = rotation;
                grass.transform.localScale = new Vector3(scale, height, scale);

                MeshFilter mf = grass.AddComponent<MeshFilter>();
                mf.mesh = grassMesh;

                MeshRenderer mr = grass.AddComponent<MeshRenderer>();
                mr.material = grassMaterial != null ? grassMaterial : CreateGrassMaterial();

                spawned++;
            }
        }

        Debug.Log($"Spawned {spawned} grass tufts");
    }

    void SpawnBushes()
    {
        int spawned = 0;
        int attempts = 0;
        int maxAttempts = bushCount * 4;

        while (spawned < bushCount && attempts < maxAttempts)
        {
            attempts++;
            
            float angle = Random.Range(0f, 360f);
            float distance = Random.Range(0f, spawnRadius * 0.9f);
            
            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * distance;
            float z = Mathf.Sin(angle * Mathf.Deg2Rad) * distance;
            
            // Use higher noise threshold for bushes (sparser)
            float noiseVal = Mathf.PerlinNoise((x / spawnRadius + 1f) * 0.5f * noiseScale * 0.7f, 
                                              (z / spawnRadius + 1f) * 0.5f * noiseScale * 0.7f);
            
            if (noiseVal > densityThreshold + 0.15f)
            {
                Vector3 position = new Vector3(x, 0, z);
                Quaternion rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                float scale = Random.Range(bushScaleMin, bushScaleMax);

                GameObject bush = new GameObject($"Bush_{spawned}");
                bush.transform.SetParent(bushContainer.transform);
                bush.transform.localPosition = position;
                bush.transform.localRotation = rotation;
                bush.transform.localScale = new Vector3(scale, scale, scale);

                MeshFilter mf = bush.AddComponent<MeshFilter>();
                mf.mesh = bushMesh;

                MeshRenderer mr = bush.AddComponent<MeshRenderer>();
                mr.material = bushMaterial != null ? bushMaterial : CreateBushMaterial();

                spawned++;
            }
        }

        Debug.Log($"Spawned {spawned} bushes");
    }

    Material CreateGrassMaterial()
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.name = "GrassMaterial";
        mat.color = new Color(0.2f, 0.4f, 0.15f, 1f);
        mat.SetFloat("_Glossiness", 0.3f);
        mat.SetFloat("_Metallic", 0f);
        return mat;
    }

    Material CreateBushMaterial()
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.name = "BushMaterial";
        mat.color = new Color(0.1f, 0.28f, 0.08f, 1f);
        mat.SetFloat("_Glossiness", 0.25f);
        mat.SetFloat("_Metallic", 0f);
        return mat;
    }

    public void ClearVegetation()
    {
        if (grassContainer != null) Destroy(grassContainer);
        if (bushContainer != null) Destroy(bushContainer);
        
        grassContainer = null;
        bushContainer = null;
    }

    public void RegenerateVegetation()
    {
        ClearVegetation();
        InitializeVegetation();
    }
}
