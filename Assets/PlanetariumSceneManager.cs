// using UnityEngine;

// /// <summary>
// /// Scene Manager - Orchestrates all environmental effects for a beautiful night planetarium scene.
// /// Manages sky dome, atmospheric effects, lighting, and integrates with PlanetariumManager.
// /// </summary>
// public class PlanetariumSceneManager : MonoBehaviour
// {
//     [Header("Scene Components")]
//     public bool createEnvironmentAutomatically = true;

//     [Header("Lighting")]
//     public bool createDirectionalLight = true;
//     public float directionalIntensity = 0.2f;
//     public Color directionalColor = new Color(0.8f, 0.85f, 1f);
    
//     public bool createAmbientLight = true;
//     public float ambientIntensity = 0.3f;

//     [Header("Camera Setup")]
//     public float cameraHeight = 1.8f;
//     public bool positionCameraAtStart = false;

//     private GameObject environmentContainer;
//     private NightSkyDome skyDome;
//     private AtmosphericEffects atmosphericFX;
//     private Light mainLight;

//     void Start()
//     {
//         if (createEnvironmentAutomatically)
//         {
//             CreateEnvironment();
//         }
//     }

//     void CreateEnvironment()
//     {
//         Debug.Log("=== Creating Planetarium Scene Environment ===");

//         // Create container
//         environmentContainer = new GameObject("PlanetariumEnvironment");
//         environmentContainer.transform.SetParent(transform);
//         environmentContainer.transform.localPosition = Vector3.zero;

//         // Setup lighting
//         SetupLighting();

//         // Create night sky
//         GameObject skyObject = new GameObject("SkyDome");
//         skyObject.transform.SetParent(environmentContainer.transform);
//         skyDome = skyObject.AddComponent<NightSkyDome>();

//         // Create atmospheric effects
//         GameObject atmosphereObject = new GameObject("Atmosphere");
//         atmosphereObject.transform.SetParent(environmentContainer.transform);
//         atmosphericFX = atmosphereObject.AddComponent<AtmosphericEffects>();

//         // Position camera
//         if (positionCameraAtStart && Camera.main != null)
//         {
//             Camera.main.transform.position = new Vector3(0, cameraHeight, 0);
//         }

//         Debug.Log("=== Planetarium Scene Ready ===");
//     }

//     void SetupLighting()
//     {
//         if (createDirectionalLight)
//         {
//             GameObject lightObj = new GameObject("DirectionalLight");
//             lightObj.transform.SetParent(environmentContainer.transform);
//             lightObj.transform.localPosition = Vector3.zero;
//             lightObj.transform.localRotation = Quaternion.Euler(45f, -45f, 0f);

//             mainLight = lightObj.AddComponent<Light>();
//             mainLight.type = LightType.Directional;
//             mainLight.intensity = directionalIntensity;
//             mainLight.color = directionalColor;
//             mainLight.shadowStrength = 0.5f;
//             mainLight.shadows = LightShadows.Soft;
//         }

//         if (createAmbientLight)
//         {
//             RenderSettings.ambientMode = AmbientMode.Flat;
//             RenderSettings.ambientLight = new Color(0.3f, 0.32f, 0.35f) * ambientIntensity;
//         }
//     }

//     /// <summary>
//     /// Transitions the scene to different times of day
//     /// </summary>
//     public void SetTimeOfDay(float timeValue)
//     {
//         // timeValue: 0 = midnight, 0.5 = noon, 1 = next midnight
//         if (skyDome != null)
//         {
//             skyDome.SetNightIntensity(1f - Mathf.Abs(timeValue - 0.5f) * 2f);
//         }
//     }

//     /// <summary>
//     /// Adjust weather/atmosphere intensity
//     /// </summary>
//     public void SetAtmosphericDensity(float density)
//     {
//         // density: 0 = clear, 1 = very foggy
//         if (RenderSettings.fogDensity != density)
//         {
//             RenderSettings.fogDensity = Mathf.Lerp(0.005f, 0.05f, density);
//         }
//     }

//     public void ToggleFog(bool enabled)
//     {
//         RenderSettings.fog = enabled;
//     }
// }
