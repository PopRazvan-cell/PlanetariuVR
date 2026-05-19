// using UnityEngine;

// /// <summary>
// /// Creates a night sky dome with realistic atmosphere, stars, and moon.
// /// Integrates with PlanetariumManager for seamless star visualization.
// /// </summary>
// public class NightSkyDome : MonoBehaviour
// {
//     [Header("Sky Dome Settings")]
//     public float skyDomeRadius = 1500f;
//     public Color nightSkyColor = new Color(0.05f, 0.05f, 0.1f, 1f);
//     public Color horizonColor = new Color(0.1f, 0.08f, 0.15f, 1f);

//     [Header("Moon")]
//     public bool showMoon = true;
//     public float moonSize = 50f;
//     public Color moonColor = new Color(0.95f, 0.93f, 0.85f, 1f);
//     public Vector3 moonDirection = new Vector3(-0.3f, 0.6f, -0.5f);

//     [Header("Atmosphere")]
//     public bool enableAtmosphericScattering = true;
//     public Color atmosphereColor = new Color(0.2f, 0.15f, 0.3f, 0.3f);
//     public float atmosphereDensity = 0.4f;

//     [Header("Stars")]
//     public bool showBackgroundStars = true;
//     public int backgroundStarCount = 200;
//     public float starSize = 3f;

//     private GameObject skyDomeObject;
//     private GameObject moonObject;
//     private Material skyMaterial;

//     void Start()
//     {
//         CreateSkyDome();
//         if (showMoon) CreateMoon();
//     }

//     void CreateSkyDome()
//     {
//         skyDomeObject = new GameObject("NightSkyDome");
//         skyDomeObject.transform.SetParent(transform);
//         skyDomeObject.transform.localPosition = Vector3.zero;

//         // Create sphere for sky
//         Mesh sphereMesh = CreateSkySphereMesh();
//         MeshFilter mf = skyDomeObject.AddComponent<MeshFilter>();
//         mf.mesh = sphereMesh;

//         // Create material
//         skyMaterial = new Material(Shader.Find("Standard"));
//         skyMaterial.name = "NightSkyMaterial";
//         skyMaterial.SetColor("_Color", nightSkyColor);
//         skyMaterial.SetFloat("_Glossiness", 1f);
//         skyMaterial.SetFloat("_Metallic", 0f);
//         skyMaterial.renderQueue = 1000;

//         MeshRenderer mr = skyDomeObject.AddComponent<MeshRenderer>();
//         mr.material = skyMaterial;

//         // Flip normals so we see inside
//         MeshCollider mc = skyDomeObject.AddComponent<MeshCollider>();
//         mc.convex = true;

//         // Add gradient stars
//         if (showBackgroundStars)
//             SpawnBackgroundStars();

//         Debug.Log("Night Sky Dome created with radius " + skyDomeRadius);
//     }

//     Mesh CreateSkySphereMesh()
//     {
//         Mesh mesh = new Mesh();
//         mesh.name = "SkyDomeMesh";

//         int segments = 32;
//         int rings = 16;

//         Vector3[] vertices = new Vector3[(segments + 1) * (rings + 1)];
//         int[] triangles = new int[segments * rings * 6];
//         Color[] colors = new Color[vertices.Length];

//         for (int ring = 0; ring <= rings; ring++)
//         {
//             float ringPercent = (float)ring / rings;
//             float phi = ringPercent * Mathf.PI;

//             for (int seg = 0; seg <= segments; seg++)
//             {
//                 float segPercent = (float)seg / segments;
//                 float theta = segPercent * Mathf.PI * 2f;

//                 float x = Mathf.Sin(phi) * Mathf.Cos(theta);
//                 float y = Mathf.Cos(phi);
//                 float z = Mathf.Sin(phi) * Mathf.Sin(theta);

//                 int idx = ring * (segments + 1) + seg;
//                 vertices[idx] = new Vector3(x, y, z) * skyDomeRadius;

//                 // Color gradient from night sky to horizon
//                 Color vertexColor = Color.Lerp(horizonColor, nightSkyColor, ringPercent * 0.5f + 0.5f);
//                 colors[idx] = vertexColor;
//             }
//         }

//         int triIdx = 0;
//         for (int ring = 0; ring < rings; ring++)
//         {
//             for (int seg = 0; seg < segments; seg++)
//             {
//                 int a = ring * (segments + 1) + seg;
//                 int b = a + 1;
//                 int c = a + (segments + 1);
//                 int d = c + 1;

//                 triangles[triIdx++] = a;
//                 triangles[triIdx++] = c;
//                 triangles[triIdx++] = b;

//                 triangles[triIdx++] = b;
//                 triangles[triIdx++] = c;
//                 triangles[triIdx++] = d;
//             }
//         }

//         mesh.vertices = vertices;
//         mesh.triangles = triangles;
//         mesh.colors = colors;
//         mesh.RecalculateNormals();

//         return mesh;
//     }

//     void SpawnBackgroundStars()
//     {
//         GameObject starsContainer = new GameObject("BackgroundStars");
//         starsContainer.transform.SetParent(skyDomeObject.transform);

//         for (int i = 0; i < backgroundStarCount; i++)
//         {
//             float theta = Random.Range(0f, Mathf.PI * 2f);
//             float phi = Random.Range(0.2f, Mathf.PI * 0.8f);

//             float x = Mathf.Sin(phi) * Mathf.Cos(theta);
//             float y = Mathf.Cos(phi);
//             float z = Mathf.Sin(phi) * Mathf.Sin(theta);

//             Vector3 starPos = new Vector3(x, y, z) * (skyDomeRadius * 0.95f);

//             GameObject starObj = new GameObject($"Star_{i}");
//             starObj.transform.SetParent(starsContainer.transform);
//             starObj.transform.localPosition = starPos;

//             float brightness = Random.Range(0.6f, 1f);
//             Color starColor = Color.Lerp(new Color(1f, 0.95f, 0.9f), Color.white, Random.value);

//             Light starLight = starObj.AddComponent<Light>();
//             starLight.type = LightType.Point;
//             starLight.range = Random.Range(20f, 100f);
//             starLight.intensity = brightness * 0.15f;
//             starLight.color = starColor;

//             // Twinkle effect
//             starObj.AddComponent<StarTwinkle>();
//         }
//     }

//     void CreateMoon()
//     {
//         moonObject = new GameObject("Moon");
//         moonObject.transform.SetParent(transform);
//         moonObject.transform.localPosition = moonDirection.normalized * skyDomeRadius * 0.85f;

//         // Create quad for moon
//         Mesh moonMesh = CreateQuadMesh(moonSize);
//         MeshFilter mf = moonObject.AddComponent<MeshFilter>();
//         mf.mesh = moonMesh;

//         Material moonMat = new Material(Shader.Find("Standard"));
//         moonMat.name = "MoonMaterial";
//         moonMat.SetColor("_Color", moonColor);
//         moonMat.SetFloat("_Glossiness", 0.8f);
//         moonMat.SetFloat("_Metallic", 0.3f);

//         MeshRenderer mr = moonObject.AddComponent<MeshRenderer>();
//         mr.material = moonMat;

//         // Add glow
//         Light moonLight = moonObject.AddComponent<Light>();
//         moonLight.type = LightType.Point;
//         moonLight.range = 500f;
//         moonLight.intensity = 0.6f;
//         moonLight.color = new Color(0.9f, 0.88f, 0.8f);

//         Debug.Log("Moon created");
//     }

//     Mesh CreateQuadMesh(float size)
//     {
//         Mesh mesh = new Mesh();
//         float half = size * 0.5f;

//         Vector3[] vertices = new Vector3[4]
//         {
//             new Vector3(-half, -half, 0),
//             new Vector3(half, -half, 0),
//             new Vector3(-half, half, 0),
//             new Vector3(half, half, 0)
//         };

//         int[] triangles = new int[6]
//         {
//             0, 2, 1,
//             1, 2, 3
//         };

//         Vector2[] uv = new Vector2[4]
//         {
//             new Vector2(0, 0),
//             new Vector2(1, 0),
//             new Vector2(0, 1),
//             new Vector2(1, 1)
//         };

//         mesh.vertices = vertices;
//         mesh.triangles = triangles;
//         mesh.uv = uv;
//         mesh.RecalculateNormals();

//         return mesh;
//     }

//     public void SetNightIntensity(float intensity)
//     {
//         if (skyMaterial != null)
//         {
//             skyMaterial.SetColor("_Color", Color.Lerp(nightSkyColor * 0.3f, nightSkyColor, intensity));
//         }
//     }
// }
