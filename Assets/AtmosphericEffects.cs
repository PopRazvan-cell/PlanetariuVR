// using UnityEngine;

// /// <summary>
// /// Creates atmospheric particle effects: dust, fog rays, light scattering.
// /// Enhances night sky with realistic atmosphere.
// /// </summary>
// public class AtmosphericEffects : MonoBehaviour
// {
//     [Header("Dust Particles")]
//     public bool enableDustParticles = true;
//     public int dustParticleCount = 500;
//     public float dustParticleSize = 0.2f;
//     public float dustDensity = 50f;
//     public Color dustColor = new Color(0.8f, 0.7f, 0.6f, 0.3f);

//     [Header("Light Rays")]
//     public bool enableLightRays = true;
//     public int lightRayCount = 8;
//     public float rayLength = 300f;
//     public float rayThickness = 5f;
//     public Color rayColor = new Color(0.9f, 0.85f, 0.7f, 0.15f);
//     public Vector3 rayDirection = new Vector3(0.2f, -0.5f, 0.3f);

//     [Header("Fog/Haze")]
//     public bool enableFog = true;
//     public float fogDensity = 0.02f;
//     public Color fogColor = new Color(0.15f, 0.12f, 0.2f);

//     [Header("Fireflies/Bioluminescence")]
//     public bool enableFireflies = true;
//     public int fireflyCount = 150;
//     public float fireflyBrightness = 0.4f;
//     public float fireflyRange = 50f;
//     public Color fireflyColor = new Color(0.6f, 0.8f, 0.4f);

//     private ParticleSystem dustParticles;
//     private ParticleSystem fireflyParticles;
//     private GameObject lightRaysContainer;

//     void Start()
//     {
//         SetupFog();
//         if (enableDustParticles) CreateDustParticles();
//         if (enableLightRays) CreateLightRays();
//         if (enableFireflies) CreateFireflies();
//     }

//     void SetupFog()
//     {
//         if (!enableFog) return;

//         RenderSettings.fog = true;
//         RenderSettings.fogMode = FogMode.ExponentialSquared;
//         RenderSettings.fogColor = fogColor;
//         RenderSettings.fogDensity = fogDensity;
//     }

//     void CreateDustParticles()
//     {
//         GameObject dustObj = new GameObject("DustParticles");
//         dustObj.transform.SetParent(transform);
//         dustObj.transform.localPosition = Vector3.zero;

//         dustParticles = dustObj.AddComponent<ParticleSystem>();

//         // Main module
//         ParticleSystem.MainModule main = dustParticles.main;
//         main.duration = 100f;
//         main.loop = true;
//         main.startLifetime = 15f;
//         main.startSpeed = 0.5f;
//         main.startSize = dustParticleSize;
//         main.startColor = new ParticleSystem.MinMaxGradient(dustColor);
//         main.maxParticles = dustParticleCount;

//         // Emission
//         ParticleSystem.EmissionModule emission = dustParticles.emission;
//         emission.rateOverTime = dustParticleCount / 15f;

//         // Shape
//         ParticleSystem.ShapeModule shape = dustParticles.shape;
//         shape.shapeType = ParticleSystemShapeType.Sphere;
//         shape.radius = dustDensity;

//         // Velocity over lifetime
//         ParticleSystem.VelocityOverLifetimeModule vel = dustParticles.velocityOverLifetime;
//         vel.enabled = true;
//         vel.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
//         vel.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.3f);
//         vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

//         // Size over lifetime
//         ParticleSystem.SizeOverLifetimeModule sizeOverLife = dustParticles.sizeOverLifetime;
//         sizeOverLife.enabled = true;
//         sizeOverLife.size = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);

//         // Fade out
//         ParticleSystem.ColorOverLifetimeModule colorOverLife = dustParticles.colorOverLifetime;
//         colorOverLife.enabled = true;
//         Gradient fadeGradient = new Gradient();
//         fadeGradient.SetKeys(
//             new GradientColorKey[] { new GradientColorKey(dustColor, 0f), new GradientColorKey(dustColor, 0.8f), new GradientColorKey(new Color(dustColor.r, dustColor.g, dustColor.b, 0f), 1f) },
//             new GradientAlphaKey[] { new GradientAlphaKey(0.3f, 0f), new GradientAlphaKey(0.3f, 0.8f), new GradientAlphaKey(0f, 1f) }
//         );
//         colorOverLife.color = new ParticleSystem.MinMaxGradient(fadeGradient);

//         // Renderer
//         ParticleSystemRenderer psr = dustObj.GetComponent<ParticleSystemRenderer>();
//         psr.renderMode = ParticleSystemRenderMode.Billboard;
//         psr.material = new Material(Shader.Find("Particles/Standard Unlit"));
//         psr.material.SetColor("_Color", dustColor);

//         Debug.Log("Dust particles created: " + dustParticleCount);
//     }

//     void CreateLightRays()
//     {
//         lightRaysContainer = new GameObject("LightRays");
//         lightRaysContainer.transform.SetParent(transform);
//         lightRaysContainer.transform.localPosition = Vector3.zero;

//         Vector3 normalizedDir = rayDirection.normalized;

//         for (int i = 0; i < lightRayCount; i++)
//         {
//             float angle = (360f / lightRayCount) * i;
//             Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
//             Vector3 rayDir = rotation * normalizedDir;

//             GameObject rayObj = new GameObject($"LightRay_{i}");
//             rayObj.transform.SetParent(lightRaysContainer.transform);
//             rayObj.transform.localPosition = Vector3.zero;
//             rayObj.transform.localRotation = Quaternion.LookRotation(rayDir);

//             // Create ray as a stretched quad
//             Mesh rayMesh = CreateRayMesh(rayLength, rayThickness);
//             MeshFilter mf = rayObj.AddComponent<MeshFilter>();
//             mf.mesh = rayMesh;

//             Material rayMat = new Material(Shader.Find("Standard"));
//             rayMat.SetColor("_Color", rayColor);
//             rayMat.SetFloat("_Glossiness", 0f);
//             rayMat.SetFloat("_Metallic", 0f);
//             rayMat.renderQueue = 3000;
//             rayMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
//             rayMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
//             rayMat.SetInt("_ZWrite", 0);
//             rayMat.EnableKeyword("_ALPHABLEND_ON");

//             MeshRenderer mr = rayObj.AddComponent<MeshRenderer>();
//             mr.material = rayMat;

//             // Add floating animation
//             rayObj.AddComponent<FloatingRayAnimation>();
//         }

//         Debug.Log("Light rays created: " + lightRayCount);
//     }

//     void CreateFireflies()
//     {
//         GameObject fireflyObj = new GameObject("Fireflies");
//         fireflyObj.transform.SetParent(transform);
//         fireflyObj.transform.localPosition = Vector3.zero;

//         fireflyParticles = fireflyObj.AddComponent<ParticleSystem>();

//         // Main module
//         ParticleSystem.MainModule main = fireflyParticles.main;
//         main.duration = 100f;
//         main.loop = true;
//         main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
//         main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
//         main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
//         main.startColor = new ParticleSystem.MinMaxGradient(fireflyColor);
//         main.maxParticles = fireflyCount;

//         // Emission
//         ParticleSystem.EmissionModule emission = fireflyParticles.emission;
//         emission.rateOverTime = fireflyCount / 3f;

//         // Shape - spawn in a large sphere
//         ParticleSystem.ShapeModule shape = fireflyParticles.shape;
//         shape.shapeType = ParticleSystemShapeType.Sphere;
//         shape.radius = 80f;

//         // Velocity
//         ParticleSystem.VelocityOverLifetimeModule vel = fireflyParticles.velocityOverLifetime;
//         vel.enabled = true;
//         vel.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
//         vel.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.5f);
//         vel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

//         // Size over lifetime - pulse effect
//         ParticleSystem.SizeOverLifetimeModule sizeOverLife = fireflyParticles.sizeOverLifetime;
//         sizeOverLife.enabled = true;
//         Curve pulseSize = new Curve();
//         AnimationCurve sizeCurve = AnimationCurve.Linear(0, 0.5f, 1, 1.5f);
//         sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

//         // Fade in and out
//         ParticleSystem.ColorOverLifetimeModule colorOverLife = fireflyParticles.colorOverLifetime;
//         colorOverLife.enabled = true;
//         Gradient fadeGradient = new Gradient();
//         fadeGradient.SetKeys(
//             new GradientColorKey[] { new GradientColorKey(fireflyColor, 0f), new GradientColorKey(fireflyColor, 0.5f), new GradientColorKey(fireflyColor, 1f) },
//             new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(fireflyBrightness, 0.5f), new GradientAlphaKey(0f, 1f) }
//         );
//         colorOverLife.color = new ParticleSystem.MinMaxGradient(fadeGradient);

//         // Renderer - additive for glow
//         ParticleSystemRenderer psr = fireflyObj.GetComponent<ParticleSystemRenderer>();
//         psr.renderMode = ParticleSystemRenderMode.Billboard;
//         Material fireflyMat = new Material(Shader.Find("Particles/Standard Unlit"));
//         fireflyMat.SetColor("_Color", fireflyColor);
//         fireflyMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
//         fireflyMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
//         psr.material = fireflyMat;

//         Debug.Log("Fireflies created: " + fireflyCount);
//     }

//     Mesh CreateRayMesh(float length, float thickness)
//     {
//         Mesh mesh = new Mesh();
        
//         float halfThickness = thickness * 0.5f;

//         Vector3[] vertices = new Vector3[4]
//         {
//             new Vector3(-halfThickness, 0, 0),
//             new Vector3(halfThickness, 0, 0),
//             new Vector3(-halfThickness, 0, length),
//             new Vector3(halfThickness, 0, length)
//         };

//         int[] triangles = new int[6]
//         {
//             0, 2, 1,
//             1, 2, 3
//         };

//         mesh.vertices = vertices;
//         mesh.triangles = triangles;
//         mesh.RecalculateNormals();

//         return mesh;
//     }
// }

// /// <summary>
// /// Simple floating animation for light rays
// /// </summary>
// public class FloatingRayAnimation : MonoBehaviour
// {
//     private float startTime;
//     private Vector3 startPos;
//     private float speed = 0.5f;
//     private float amplitude = 2f;

//     void Start()
//     {
//         startTime = Time.time;
//         startPos = transform.localPosition;
//     }

//     void Update()
//     {
//         float elapsed = Time.time - startTime;
//         float offset = Mathf.Sin(elapsed * speed) * amplitude;
//         transform.localPosition = startPos + Vector3.up * offset;
//     }
// }
