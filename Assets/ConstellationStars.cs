using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ConstellationStars : MonoBehaviour
{
    public PlanetariumManager planetariumManager;

    [Header("Star Appearance")]
    public GameObject starPrefab;
    public float starScale = 8f;
    public Color starColor = Color.white;

    [Header("Line Appearance")]
    public Color lineColor = new Color(0.3f, 0.7f, 1f, 0.7f);
    public float lineWidth = 2f;
    public Material lineMaterial;

    [Header("Debug")]
    public bool drawLabels = false;
    public float labelFontSize = 6f;

    private List<GameObject> starObjects = new List<GameObject>();
    private List<LineRenderer> lineRenderers = new List<LineRenderer>();
    private List<ConstLine> activeLines = new List<ConstLine>();
    private bool constellationsVisible = true;

    private struct ConstLine
    {
        public LineRenderer renderer;
        public Transform t1;
        public Transform t2;
    }

    void Start()
    {
        if (planetariumManager == null)
            planetariumManager = FindObjectOfType<PlanetariumManager>();

        if (planetariumManager == null)
        {
            Debug.LogError("ConstellationStars: PlanetariumManager nu a fost găsit!");
            return;
        }

        CreateConstellationStars();
        CreateConstellationLines();
    }

    void CreateConstellationStars()
    {
        foreach (var cstar in constellationStars)
        {
            GameObject star = starPrefab != null ? Instantiate(starPrefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            star.name = "ConstStar_" + cstar.id;
            star.transform.SetParent(transform, false);
            star.transform.localScale = Vector3.one * starScale * Mathf.Clamp(6f - cstar.mag, 1f, 5f);

            if (starPrefab == null)
            {
                var renderer = star.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = starColor;
            }

            starObjects.Add(star);

            // Inițial în origine, Update() le poziționează
            star.transform.position = Vector3.zero;

            if (drawLabels)
            {
                GameObject labelObj = new GameObject("Label_" + cstar.id);
                labelObj.transform.SetParent(star.transform, false);
                labelObj.transform.localPosition = Vector3.up * 12f;
                var tmp = labelObj.AddComponent<TextMeshPro>();
                tmp.text = cstar.id;
                tmp.fontSize = labelFontSize;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }
        }
    }

    void CreateConstellationLines()
    {
        // Construim un dicționar id→transform
        Dictionary<string, Transform> starMap = new Dictionary<string, Transform>();
        for (int i = 0; i < constellationStars.Length; i++)
            starMap[constellationStars[i].id] = starObjects[i].transform;

        foreach (var seg in constellationSegments)
        {
            if (!starMap.TryGetValue(seg.id1, out Transform t1)) continue;
            if (!starMap.TryGetValue(seg.id2, out Transform t2)) continue;

            GameObject lineObj = new GameObject($"Line_{seg.id1}-{seg.id2}");
            lineObj.transform.SetParent(transform, false);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, t1.position);
            lr.SetPosition(1, t2.position);

            if (lineMaterial != null)
                lr.material = lineMaterial;
            else
            {
                var mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = lineColor;
                lr.material = mat;
            }
            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            activeLines.Add(new ConstLine { renderer = lr, t1 = t1, t2 = t2 });
            lineRenderers.Add(lr);
        }

        Debug.Log($"ConstellationStars: {constellationStars.Length} stele, {activeLines.Count} linii");
    }

    public void SetConstellationsVisible(bool visible)
    {
        constellationsVisible = visible;
        foreach (var star in starObjects)
        {
            if (star != null)
                star.SetActive(visible);
        }
        foreach (var lr in lineRenderers)
        {
            if (lr != null)
                lr.enabled = visible;
        }
    }

    void Update()
    {
        if (planetariumManager == null) return;
        if (!constellationsVisible) return;

        float lat = planetariumManager.latitude;
        float lon = planetariumManager.longitude;
        float radius = planetariumManager.radius;

        // Folosim același timp ca PlanetariumManager
        var currentTime = GetSimulatedTime();
        double jd = AstroMath.CalculateJulianDate(currentTime);
        double lst = AstroMath.CalculateLocalSiderealTime(jd, lon);

        for (int i = 0; i < constellationStars.Length; i++)
        {
            var cs = constellationStars[i];
            Transform t = starObjects[i].transform;

            AstroMath.EquatorialToHorizontal(cs.ra, cs.dec, lat, lst, out float altRad, out float azRad);
            float altDeg = altRad * Mathf.Rad2Deg;
            if (altDeg < 0f)
            {
                t.position = Vector3.zero;
                t.gameObject.SetActive(false);
                continue;
            }
            t.gameObject.SetActive(true);
            t.position = AstroMath.SphericalToCartesian(altRad, azRad, radius);
        }

        foreach (var line in activeLines)
        {
            if (line.renderer == null) continue;
            bool visible = line.t1.gameObject.activeSelf && line.t2.gameObject.activeSelf;
            line.renderer.enabled = visible;
            if (visible)
            {
                line.renderer.SetPosition(0, line.t1.position);
                line.renderer.SetPosition(1, line.t2.position);
            }
        }
    }

    DateTime GetSimulatedTime()
    {
        var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var field = typeof(PlanetariumManager).GetField("currentTime", bindingFlags);
        if (field != null)
            return (DateTime)field.GetValue(planetariumManager);
        return DateTime.UtcNow;
    }

    // ═══════════════════════════════════════════
    // DATE CONSTELAȚII: stele + segmente
    // RA/Dec în grade (J2000)
    // ═══════════════════════════════════════════

    private struct ConstStarData
    {
        public string id;
        public float ra;  // grade
        public float dec; // grade
        public float mag;
        public ConstStarData(string id, float ra, float dec, float mag)
        {
            this.id = id; this.ra = ra; this.dec = dec; this.mag = mag;
        }
    }

    private struct SegData
    {
        public string id1;
        public string id2;
        public SegData(string a, string b) { id1 = a; id2 = b; }
    }

    // Coordonate preluate din răspunsul API (J2000, grade)
    private static readonly ConstStarData[] constellationStars = new ConstStarData[]
    // === star,ra,dec,luminosity
    {
        // === Ursa Major (Carul Mare) ===
        new("alf UMa", 166.34f, 61.61f, 1.8f),
        new("bet UMa", 165.85f, 56.24f, 2.4f),
        new("gam UMa", 178.80f, 53.55f, 2.4f),
        new("del UMa", 184.20f, 56.89f, 3.3f),
        new("eps UMa", 193.80f, 55.82f, 1.8f),
        new("zet UMa", 201.25f, 54.79f, 2.1f),
        new("eta UMa", 207.15f, 49.18f, 1.9f),

        // === Ursa Minor (Carul Mic) ===
        new("alf UMi", 46.25f, 89.37f, 2.0f),
        new("bet UMi", 222.68f, 74.05f, 2.1f),
        new("gam UMi", 230.19f, 71.74f, 3.0f),
        new("del UMi", 261.04f, 86.56f, 4.4f),
        new("eps UMi", 250.87f, 81.99f, 4.2f),
        new("zet UMi", 235.82f, 77.71f, 4.3f),
        new("eta UMi", 244.21f, 75.69f, 4.9f),

        // === Cassiopeia ===
        new("alf Cas", 10.50f, 56.67f, 2.2f),
        new("bet Cas", 2.65f, 59.29f, 2.3f),
        new("gam Cas", 14.58f, 60.85f, 2.5f),
        new("del Cas", 21.75f, 60.36f, 2.7f),
        new("eps Cas", 28.60f, 63.67f, 3.4f),


        // === Cepheus ===
        new("alf Cep", 319.80f, 62.69f, 2.4f),
        new("bet Cep", 322.25f, 70.67f, 3.2f),
        new("gam Cep", 355.11f, 77.77f, 3.2f),
        new("iota Cep", 342.6f, 66.33f, 4.1f),
        new("zet Cep", 332.85f, 58.32f, 4.2f),
        
       

        // === Orion ===
        new("lam Ori", 84.14f,  9.95f, 3.5f),
        new("alf Ori", 89.14f,  7.41f, 0.5f),
        new("gam Ori", 81.63f,  6.37f, 1.6f),

        new("del Ori", 83.33f, -0.27f, 2.2f),
        new("eps Ori", 84.38f, -1.18f, 1.7f),
        new("zet Ori", 85.52f, -1.92f, 1.8f),

        new("kap Ori", 87.25f, -9.65f, 2.1f),
        new("bet Ori", 78.94f, -8.17f, 0.1f),

        new("pi6 Ori", 74.98f,  1.75f, 4.5f),
        new("pi5 Ori", 73.90f,  2.48f, 3.7f),
        new("pi4 Ori", 73.15f,  5.65f, 3.7f),
        new("pi3 Ori", 72.81f,  7.00f, 3.2f),
        new("pi2 Ori", 73.01f,  8.94f, 4.4f),
        new("pi1 Ori", 74.08f, 10.19f, 4.7f),

        // === Canis Major ===
        new("alf CMa", 101.57f, -16.75f, -1.5f), // Sirius
        new("bet CMa",  95.96f, -17.96f,  2.0f), // Mirzam
        new("gam CMa", 106.24f, -15.67f,  4.1f), // Muliphein
        new("the CMa", 103.85f, -12.07f,  4.1f), // Theta CMa
        new("omi2 CMa",106.03f, -23.87f,  3.0f), // Omicron2
        new("del CMa", 107.36f, -26.43f,  1.8f), // Wezen
        new("eps CMa", 104.91f, -29.00f,  1.5f), // Adhara
        new("eta CMa", 111.28f, -29.35f,  2.5f), // Aludra
        new("zet CMa",  95.32f, -30.07f,  3.0f), // Furud
        new("sig CMa",  105.69f, -27.97f,  3.58f), 
        new("omi1 CMa",  103.80f, -24.21f,  4.0f), 
        new("v2 CMa", 99.45f, -19.28f,  4.09f),
        new("xi2 CMa",  99.04f, -22.98f,  4.53f),
        new("iot CMa",  104.32f, -17.09f,  4.34f), 
        new("K CMa",  102.70f, -32.54f,  3.45f), 


        // === Canis Minor ===
        new("alf CMi", 114.83f, 5.22f, 0.4f),
        new("bet CMi", 111.79f, 8.29f, 2.9f),
         
       

        // === Gemini ===
        new("alf Gem", 113.65f, 31.89f, 1.58f), // Castor
        new("bet Gem", 116.33f, 28.03f, 1.16f), // Pollux

        new("the Gem", 103.20f, 33.96f, 3.60f), // θ Gem
        new("tau Gem", 107.78f, 30.25f, 4.41f), // τ Gem
        new("iot Gem", 111.43f, 27.80f, 3.78f), // ι Gem
        new("ups Gem", 113.98f, 26.90f, 4.06f), // υ Gem
        new("kap Gem", 116.11f, 24.40f, 3.57f), // κ Gem

        new("del Gem", 110.03f, 21.98f, 3.50f), // Wasat
        new("lam Gem", 109.52f, 16.54f, 3.58f), // λ Gem
        new("zet Gem", 106.03f, 20.57f, 4.01f), // Mekbuda
        new("gam Gem", 99.43f, 16.40f, 1.93f),  // Alhena
        new("xi Gem", 101.32f, 12.90f, 3.35f),  // Alzirr

        new("eps Gem", 100.98f, 25.13f, 3.06f), // Mebsuta
        new("mu Gem", 95.74f, 22.51f, 2.87f),   // Tejat
        new("nu Gem", 97.24f, 20.21f, 4.13f),   // ν Gem
        new("eta Gem", 93.72f, 22.51f, 3.31f),  // Propus
        new("1 Gem", 91.03f, 23.26f, 4.16f),    // 1 Gem


        // === Taurus ===
        new("bet Tau", 81.98f, 28.61f, 1.7f),  // Elnath
        new("zet Tau", 84.80f, 21.14f, 3.0f),  // Zeta Tauri
        new("alf Tau", 69.35f, 16.51f, 0.9f),  // Aldebaran
        new("lam Tau", 60.53f, 12.56f, 3.5f),  // Lambda Tauri
        new("the2 Tau", 67.54f, 15.92f, 3.45f),
        new("gam Tau", 65.32f, 15.69f, 3.80f),
        new("del Tau", 66.11f, 17.60f, 3.92f),
        new("eps Tau", 67.53f, 19.23f, 3.69f),
        new("xi Tau",  51.79f,  9.73f, 3.7f),  // Xi Tauri
       

    

        // === Auriga ===
        new("alf Aur", 79.39f, 46.00f, 0.1f),  // Capella
        new("bet Aur", 90.36f, 44.95f, 1.9f),  // Menkalinan
        new("zet Aur", 76.07f, 41.11f, 3.8f),  // Saclateni
        new("the Aur", 90.37f, 37.21f, 2.7f),  // Mahasim
        new("iot Aur", 74.67f, 33.21f, 2.7f),  // Hassaleh
        new("gam Aur", 81.98f, 28.61f, 1.7f),  // Elnath (istoric Gamma Aur)

        // === Leo ===
        new("alf Leo", 152.09f, 11.97f, 1.4f),
        new("bet Leo", 177.41f, 14.57f, 2.1f), // Corectat (Denebola)
        new("gam Leo", 154.90f, 19.84f, 2.0f),
        new("del Leo", 168.48f, 20.52f, 2.6f),
        new("eps Leo", 146.46f, 23.77f, 3.0f),
        new("zet Leo", 154.16f, 23.42f, 3.4f),
        new("eta Leo", 151.85f, 16.76f, 3.5f),
        new("mu Leo", 148.19f, 26.01f, 3.9f),

        // === Virgo ===
        new("alf Vir", 201.30f, -11.16f, 1.0f),
        new("gam Vir", 190.44f, -1.45f, 2.7f),
        new("del Vir", 193.92f, 3.42f, 3.4f),
        new("eps Vir", 195.54f, 10.96f, 2.8f), // Corectat (Vindemiatrix)
        new("zet Vir", 203.65f, -0.61f, 3.4f),
        new("eta Vir", 184.97f, -0.67f, 3.9f),
        new("nu Vir", 176.46f, 6.53f, 4.1f),
        new("k Vir", 213.58f, -10.39f, 4.3f),
        new("iot Vir", 214.36f, -6.12f, 4.2f),
        new("mu Vir", 221.12f, -5.77f, 3.9f),
        new("tau Vir", 210.75f, 1.41f, 4.3f),
        new("109 Vir", 221.90f, 1.78f, 3.9f),


        

        // === Boötes ===
        new("alf Boo", 214.22f, 19.05f, 0.1f),
        new("bet Boo", 225.62f, 40.29f, 3.5f),
        new("gam Boo", 218.29f, 38.19f, 3.0f),
        new("del Boo", 229.15f, 33.21f, 3.5f),
        new("eps Boo", 221.54f, 26.96f, 2.4f),
        new("zet Boo", 220.29f, 13.73f, 3.8f),
        new("eta Boo", 208.68f, 18.40f, 2.7f),
        new("rho Boo", 218.24f, 30.25f, 3.72f),

         

        // === Corona Borealis ===
        new("alf CrB", 233.96f, 26.71f, 2.2f),
        new("bet CrB", 232.24f, 29.11f, 3.7f),
        new("gam CrB", 235.68f, 26.29f, 3.8f),
        new("del CrB", 237.40f, 26.07f, 4.6f),
        new("eps CrB", 239.38f, 26.88f, 4.1f),
        new("iot CrB", 240.36f, 29.85f, 5.1f),
        new("the CrB", 233.23f, 31.36f, 5.0f),

        // === Hercules ===
        // --- THE KEYSTONE (The Torso) ---
        new("Zeta Herculis", 250.32f, 31.60f, 2.81f),
        new ("Pi Herculis", 258.76f, 36.80f, 3.16f),
        new ("Eta Herculis", 250.72f, 38.92f, 3.48f),
        new ("Epsilon Herculis", 255.07f, 30.92f, 3.92f),

            // --- HEAD, SHOULDERS & CONNECTIONS ---
        new ("Beta Herculis (Kornephoros)", 247.55f, 21.48f, 2.78f),
        new ("Delta Herculis (Sarin)", 258.75f, 24.83f, 3.12f),
        new ("Lambda Herculis", 262.68f, 26.11f, 4.41f),

        // --- ARMS & HANDS ---
        new ("Gamma Herculis", 245.48f, 19.15f, 3.74f),
        new ("Mu Herculis", 266.61f, 27.72f, 3.42f),
        new ("Xi Herculis", 269.44f, 29.25f, 3.70f),
        new ("Omicron Herculis", 271.88f, 28.76f, 3.84f),

        // --- LEGS & FEET ---
        new ("Theta Herculis", 269.06f, 37.25f, 3.86f),
        new ("Iota Herculis", 264.86f, 46.00f, 3.82f),
        new ("Tau Herculis", 244.93f, 46.31f, 3.91f),
        new ("Phi Herculis", 242.19f, 44.93f, 4.23f),
        new ("Sigma Herculis", 248.52f, 42.43f, 4.20f),

        // === Lyra ===
        new("alf Lyr", 279.23f, 38.78f, 0.0f),
        new("bet Lyr", 282.53f, 33.36f, 3.5f),
        new("gam Lyr", 284.66f, 32.69f, 3.2f),
        new("del Lyr", 283.47f, 36.98f, 4.3f),
        new("eps Lyr", 281.11f, 39.67f, 4.7f),
        new("zet Lyr", 281.17f, 37.60f, 4.3f),

        // === Cygnus ===
        new("alf Cyg", 310.35f, 45.28f, 1.3f),
        new("bet Cyg", 292.68f, 27.96f, 3.1f),
        new("gam Cyg", 305.56f, 40.26f, 2.2f),
        new("del Cyg", 296.24f, 45.13f, 2.9f),
        new("eps Cyg", 311.57f, 33.97f, 2.5f),

        // === Aquila ===
        new("Alpha Aquilae (Altair)", 297.6958f, 8.8683f, 0.76f),
        new("Gamma Aquilae (Tarazed)", 296.5650f, 10.6133f, 2.72f),
        new("Beta Aquilae (Alshain)", 298.8283f, 6.4067f, 3.71f),
        new("Zeta Aquilae (Okab)", 286.3525f, 13.8633f, 2.99f),
        new("Theta Aquilae", 302.8263f, -0.74f, 3.24f),
        new("Delta Aquilae", 291.3746f, 3.1147f, 3.36f),
        new("Lambda Aquilae", 286.5621f, -4.8825f, 3.43f),
        new("Eta Aquilae", 298.1183f, 1.0056f, 3.87f),
        new("Epsilon Aquilae", 284.9058f, 15.0683f, 4.02f),
        
         //SCORPION
        new("Alpha Scorpii", 247.35f, -26.43f, 1.06f),
        new("Lambda Scorpii", 263.40f, -37.10f, 1.62f),
        new("Theta Scorpii", 264.63f, -43.00f, 1.86f),
        new("Epsilon Scorpii", 252.54f, -34.29f, 2.29f),
        new("Delta Scorpii", 240.08f, -22.62f, 2.29f),
        new("Kappa Scorpii", 265.62f, -39.03f, 2.39f),
        new("Beta 1 Scorpii", 241.35f, -19.80f, 2.56f),
        new("Upsilon Scorpii", 262.80f, -37.30f, 2.70f),
        new("Tau Scorpii", 248.97f, -28.22f, 2.82f),
        new("Sigma Scorpii", 245.3f, -25.59f, 2.89f),
        new("Pi Scorpii", 239.71f, -26.11f, 2.89f),
        new("Iota 1 Scorpii", 266.9f, -40.13f, 2.99f),
        new("Mu 1 Scorpii", 253.05f, -38.05f, 3.00f),
        new("Eta Scorpii", 258.12f, -43.24f, 3.32f),
        new("Zeta 2 Scorpii", 253.88f, -42.36f, 3.62f),

        //SAGETATOR----
        new("Epsilon Sagittarii", 276.04f, -34.38f, 1.79f),
        new("Sigma Sagittarii", 283.82f, -26.30f, 2.05f),
        new("Zeta Sagittarii", 285.82f, -29.88f, 2.60f),
        new("Delta Sagittarii", 275.29f, -29.83f, 2.72f),
        new("Lambda Sagittarii", 277.04f, -25.42f, 2.82f),
        new("Pi Sagittarii", 287.62f, -21.02f, 2.88f),
        new("Gamma 2 Sagittarii", 271.54f, -30.42f, 2.98f),
        new("Phi Sagittarii", 281.41f, -26.99f, 3.17f),
        new("Tau Sagittarii", 286.74f, -27.67f, 3.32f),
        new("Xi 2 Sagittarii", 284.40f, -21.14f, 3.52f),
        new("Omicron Sagittarii", 286.13f, -21.74f, 3.76f),
        new("Mu Sagittarii", 273.46f, -21.06f, 3.84f),
        new("Beta 1 Sagittarii", 290.71f, -44.46f, 3.96f),
        new("Alpha Sagittarii", 290.96f, -40.61f, 3.97f),

        //PEGASUS----

        new("Alpha Pegasi", 346.18f, 15.20f, 2.49f),
        new("Beta Pegasi", 345.82f, 28.08f, 2.44f),
        new("Gamma Pegasi", 3.34f, 15.18f, 2.83f),
        new("Epsilon Pegasi", 326.05f, 9.88f, 2.38f),
        new("Zeta Pegasi", 340.37f, 10.83f, 3.41f),
        new("Eta Pegasi", 340.75f, 30.22f, 2.93f),
        new("Theta Pegasi", 332.55f, 6.20f, 3.53f),
        new("Iota Pegasi", 331.75f, 25.35f, 3.77f),
        new("Kappa Pegasi", 326.17f, 25.64f, 4.13f),
        new("Lambda Pegasi", 342.13f, 23.56f, 3.97f),
        new("Mu Pegasi", 342.50f, 24.60f, 3.51f),
        new("Nu Pegasi", 331.42f, 5.06f, 4.86f),
        new("Xi Pegasi", 341.72f, 12.10f, 4.20f),
        new("Omicron Pegasi", 340.48f, 29.31f, 4.79f),
        new("Pi 1 Pegasi", 332.48f, 33.17f, 5.58f),
        new("Pi 2 Pegasi", 332.49f, 33.18f, 4.28f),
        new("Rho Pegasi", 343.81f, 8.82f, 4.90f),
        new("Sigma Pegasi", 343.10f, 9.84f, 5.16f),
        new("Tau Pegasi", 350.16f, 23.74f, 4.58f),
        new("Upsilon Pegasi", 351.34f, 23.40f, 4.40f),
        new("Phi Pegasi", 358.12f, 19.12f, 5.08f),
        new("Chi Pegasi", 3.76f, 20.21f, 4.79f),
        new("Psi Pegasi", 359.44f, 25.14f, 4.63f),

        // ANDROMEDA
        new("Alpha Andromedae", 2.10f, 29.08f, 2.07f),
        new("Beta Andromedae", 17.56f, 35.62f, 2.07f),
        new("Gamma 1 Andromedae", 31.06f, 42.33f, 2.10f),
        new("Delta Andromedae", 9.81f, 30.87f, 3.27f),
        new("Epsilon Andromedae", 9.64f, 29.31f, 4.34f),
        new("Zeta Andromedae", 11.83f, 24.27f, 4.08f),
        new("Eta Andromedae", 14.33f, 23.42f, 4.42f),
        new("Theta Andromedae", 4.27f, 38.68f, 4.61f),
        new("Iota Andromedae", 354.53f, 43.27f, 4.29f),
        new("Kappa Andromedae", 355.23f, 44.33f, 4.15f),
        new("Lambda Andromedae", 354.88f, 46.46f, 3.82f),
        new("Mu Andromedae", 14.15f, 38.50f, 3.87f),
        new("Nu Andromedae", 12.45f, 41.07f, 4.53f),
        new("Xi Andromedae", 20.40f, 45.52f, 4.54f),
        new("Omicron Andromedae", 345.54f, 42.33f, 3.62f),
        new("Pi Andromedae", 9.22f, 33.72f, 4.41f),
        new("Rho Andromedae", 5.28f, 37.97f, 5.18f),
        new("Sigma Andromedae", 4.70f, 36.78f, 4.51f),
        new("Tau Andromedae", 25.15f, 40.58f, 4.96f),
        new("Upsilon Andromedae", 24.20f, 41.40f, 4.10f),
        new("Phi Andromedae", 17.38f, 47.24f, 4.25f),
        new("Chi Andromedae", 25.02f, 44.20f, 5.01f),

        // === Perseus ===
        new("alf Per", 51.08f, 49.86f, 1.8f),
        new("bet Per", 47.04f, 40.96f, 2.1f),
        new("gam Per", 45.79f, 53.51f, 2.9f),
        new("del Per", 55.73f, 47.71f, 3.0f),
        new("eps Per", 59.45f, 40.01f, 2.9f),
        new("zet Per", 58.55f, 31.88f, 2.9f),
          
       

        // === Triangulum ===
        new("alf Tri", 28.23f, 29.58f, 3.4f),
        new("bet Tri", 32.33f, 34.99f, 3.0f),
        new("gam Tri", 34.29f, 33.85f, 4.0f),

        // === Aries ===
        new("alf Ari", 31.79f, 23.46f, 2.0f),
        new("bet Ari", 28.65f, 20.81f, 2.6f),
        new("gam Ari", 28.48f, 19.28f, 3.9f),

        // === Cetus ===
        new("Alp Cet", 45.57f, 4.08f, 2.54f),
        new("Bet Cet", 11.05f, -17.99f, 2.04f),
        new("Gam Cet", 41.07f, 3.23f, 3.47f),
        new("Del Cet", 39.87f, 0.32f, 4.08f),
        new("Eps Cet", 39.89f, -11.87f, 4.83f),
        new("Zet Cet", 28.14f, -10.33f, 3.74f),
        new("Eta Cet", 17.15f, -10.18f, 3.46f),
        new("The Cet", 21.01f, -8.18f, 3.60f),
        new("Iot Cet", 4.86f, -8.82f, 3.56f),
        new("Lam Cet", 44.75f, 8.91f, 4.71f),
        new("Mu Cet", 41.54f, 10.11f, 4.27f),
        new("Nu Cet", 39.02f, 5.59f, 4.87f),
        new("Xi 2 Cet", 37.04f, 8.46f, 4.30f),
        new("Omi Cet", 34.83f, -2.97f, 3.04f),
        new("Pi Cet", 41.01f, -13.86f, 4.33f),
        new("Tau Cet", 26.02f, -15.93f, 3.49f),

        // === Hydra ===
        new("rho Hya", 132.11f,   5.84f, 4.3f), // Rho Hydrae
        new("eta Hya", 130.80f,   3.40f, 4.3f), // Eta Hydrae
        new("sig Hya", 129.69f,   3.34f, 4.4f), // Sigma Hydrae / Minchir
        new("del Hya", 129.41f,   5.70f, 4.1f), // Delta Hydrae
        new("eps Hya", 131.69f,   6.42f, 3.4f), // Epsilon Hydrae
        new("zet Hya", 133.85f,   5.95f, 3.1f), // Zeta Hydrae
        // === Hydra: gat + corp ===
        new("tet Hya", 138.59f,   2.31f, 3.9f), // Theta Hydrae
        new("iot Hya", 144.96f,  -1.14f, 3.9f), // Iota Hydrae
        new("alf Hya", 141.90f,  -8.66f, 2.0f), // Alphard
        new("ups1 Hya",147.87f, -14.85f, 4.1f), // Upsilon1 Hydrae
        new("ups2 Hya",151.28f, -13.06f, 4.6f), // Upsilon2 Hydrae
        new("lam Hya", 152.65f, -12.35f, 3.6f), // Lambda Hydrae
        new("mu Hya",  156.52f, -16.84f, 3.8f), // Mu Hydrae
        new("nu Hya",  162.40f, -16.19f, 3.1f), // Nu Hydrae
        // === Hydra: coada ===
        new("xi Hya",  173.25f, -31.86f, 3.5f), // Xi Hydrae
        new("bet Hya", 178.23f, -33.91f, 4.3f), // Beta Hydrae
        new("gam Hya", 199.73f, -23.17f, 3.0f), // Gamma Hydrae
        new("pi Hya",  211.59f, -26.68f, 3.3f), // Pi Hydrae
        new("58 Hya",  222.57f, -27.96f, 4.4f), // 58 Hydrae, optional pentru varful cozii

        // === Corvus ===
        new("alf Crv", 182.10f, -24.59f, 4.0f),
        new("bet Crv", 188.62f, -23.40f, 2.7f),
        new("gam Crv", 183.95f, -17.54f, 2.6f),
        new("del Crv", 187.44f, -16.51f, 3.0f),

        // === Crater ===
        new("alf Crt", 164.94f, -18.30f, 4.1f), // Alpha Crateris / Alkes
        new("bet Crt", 167.91f, -22.83f, 4.5f), // Beta Crateris
        new("gam Crt", 171.22f, -17.68f, 4.1f), // Gamma Crateris
        new("del Crt", 169.84f, -14.78f, 3.6f), // Delta Crateris, cea mai luminoasa
        new("eps Crt", 171.15f, -10.86f, 4.8f), // Epsilon Crateris, marginea cupei
        new("tet Crt", 174.17f,  -9.80f, 4.7f), // Theta Crateris
        new("zet Crt", 176.19f, -18.35f, 4.7f), // Zeta Crateris, marginea cupei
        new("eta Crt", 179.00f, -17.15f, 5.2f), // Eta Crateris

        // LIBRA
        new("Alp 2 Lib", 222.72f, -16.04f, 2.75f),
        new("Bet Lib", 229.21f, -9.38f, 2.61f),
        new("Gam Lib", 233.91f, -14.79f, 3.91f),
        new("The Lib", 238.46f, -16.73f, 4.13f),
        new("Sig Lib", 226.02f, -25.28f, 3.25f),

        // CAPRICORNUS
        new("Alp 1 Cap", 304.41f, -12.51f, 4.24f),
        new("Alp 2 Cap", 304.51f, -12.54f, 3.57f),
        new("Bet Cap", 305.25f, -14.78f, 3.08f),
        new("Gam Cap", 325.02f, -16.66f, 3.68f),
        new("Del Cap", 326.70f, -16.13f, 2.87f),
        new("Eps Cap", 324.27f, -19.47f, 4.51f),
        new("Zet Cap", 321.67f, -22.41f, 3.74f),
        new("The Cap", 316.49f, -17.23f, 4.07f),
        new("Iot Cap", 320.56f, -16.83f, 4.28f),
        new("Kap Cap", 325.66f, -18.87f, 4.72f),
        new("Lam Cap", 326.63f, -11.37f, 5.57f),
        new("Mu Cap", 328.32f, -13.55f, 5.08f),
        new("Nu Cap", 305.26f, -12.68f, 4.77f),
        new("Rho Cap", 307.21f, -17.81f, 4.77f),
        new("Psi Cap", 311.52f, -25.27f, 4.14f),
        new("Omega Cap", 312.96f, -26.92f, 4.11f),

        // AQUARIUS------
        new("Alp Aqr", 331.44f, -0.32f, 2.94f),
        new("Bet Aqr", 323.36f, -5.57f, 2.91f),
        new("Gam Aqr", 335.43f, -1.38f, 3.85f),
        new("Del Aqr", 343.68f, -15.82f, 3.27f),
        new("Eps Aqr", 312.23f, -9.50f, 3.77f),
        new("Zet 1 Aqr", 337.19f, -0.02f, 3.65f),
        new("The Aqr", 334.18f, -7.86f, 4.16f),
        new("Iot Aqr", 331.61f, -13.87f, 4.29f),
        new("Lam Aqr", 343.16f, -7.58f, 3.74f),
        new("Sigma Aqr", 337.66f, -10.68f, 4.82f),
        new("Tau 2 Aqr", 342.4f, -13.59f, 4.05f),
        new("Psi 1 Aqr", 348.97f, -9.09f, 4.24f),
        new("b1 Aqr", 351.09f, -19.95f, 4.09f),
        new("c2 Aqr", 347.2f, -21.02f, 3.84f),
        new("Eta Aqr", 338.84f, -0.12f, 4.03f),
        



        // PISCES
        new("Alp Psc", 30.51f, 2.76f, 3.82f),
        new("Gam Psc", 349.29f, 3.28f, 3.70f),
        new("Del Psc", 12.22f, 7.59f, 4.44f),
        new("Eps Psc", 15.74f, 7.89f, 4.27f),
        new("Zet Psc", 18.44f, 7.58f, 5.21f),
        new("Eta Psc", 22.92f, 15.34f, 3.62f),
        new("The Psc", 351.99f, 6.38f, 4.27f),
        new("Iot Psc", 354.99f, 5.63f, 4.13f),
        new("Kap Psc", 351.73f, 1.26f, 4.87f),
        new("Lam Psc", 355.51f, 1.78f, 4.49f),
        new("Mu Psc", 22.55f, 6.14f, 4.84f),
        new("Nu Psc", 25.36f, 5.49f, 4.88f),
        new("Xi Psc", 28.39f, 3.19f, 4.61f),
        new("Omi Psc", 26.35f, 9.16f, 4.26f),
        new("Phi Psc", 18.80f, 24.72f, 4.81f),
        new("Rho Psc", 21.56f, 19.17f, 5.35f),
        new("Sigma Psc", 15.70f, 31.80f, 5.50f),
        new("Ups Psc", 20.23f, 27.40f, 4.75f),
        new("Omega Psc", 359.81f, 6.86f, 4.03f),

        // DRACO
        new("Alp Dra", 211.10f, 64.37f, 3.67f),
        new("Bet Dra", 262.61f, 52.30f, 2.79f),
        new("Gam Dra", 269.15f, 51.49f, 2.24f),
        new("Del Dra", 287.97f, 67.66f, 3.07f),
        new("Eps Dra", 297.02f, 70.34f, 3.84f),
        new("Zet Dra", 257.19f, 65.71f, 3.17f),
        new("Eta Dra", 247.74f, 61.52f, 2.73f),
        new("The Dra", 240.47f, 58.56f, 4.01f),
        new("Iot Dra", 231.23f, 58.96f, 3.29f),
        new("Kap Dra", 188.37f, 69.78f, 3.87f),
        new("Lam Dra", 172.85f, 69.33f, 3.84f),
        new("Mu Dra", 256.44f, 54.47f, 4.91f),
        new("Nu 1 Dra", 263.04f, 55.18f, 4.88f),
        new("Xi Dra", 268.39f, 56.87f, 3.75f),
        new("Omi Dra", 282.80f, 59.38f, 4.66f),
        new("Pi Dra", 295.34f, 65.32f, 4.59f),
        new("Rho Dra", 271.61f, 70.01f, 4.51f),
        new("Sigma Dra", 293.45f, 69.66f, 4.68f),
        new("Tau Dra", 288.87f, 73.35f, 4.45f),
        new("Chi Dra", 275.26f, 72.73f, 3.57f),
        new("Omega Dra", 264.24f, 68.75f, 4.80f),
    };

    private static readonly SegData[] constellationSegments = new SegData[]
    {
        // Ursa Major (Cupa + Mânerul)
        new("alf UMa", "bet UMa"),
        new("bet UMa", "gam UMa"),
        new("gam UMa", "del UMa"),
        new("del UMa", "alf UMa"), // Închide cupa
        new("del UMa", "eps UMa"), // Baza mânerului
        new("eps UMa", "zet UMa"),
        new("zet UMa", "eta UMa"),

        // Ursa Minor (Cupa + Mânerul)
        new("alf UMi", "del UMi"),
        new("del UMi", "eps UMi"),
        new("eps UMi", "zet UMi"),
        new("zet UMi", "eta UMi"),
        new("eta UMi", "gam UMi"),
        new("gam UMi", "bet UMi"),
        new("bet UMi", "zet UMi"), // Închide cupa

        // Cassiopeia (W-ul corect)
        new("bet Cas", "alf Cas"),
        new("alf Cas", "gam Cas"),
        new("gam Cas", "del Cas"),
        new("del Cas", "eps Cas"),

        // Cepheus (Forma de casă)
        new("alf Cep", "bet Cep"),
        new("bet Cep", "gam Cep"),
        new("gam Cep", "iota Cep"),
        new("iota Cep", "zet Cep"),
        new("zet Cep", "alf Cep"),

        // Orion (Corpul și Centura)
        // === Orion ===
        new("lam Ori", "alf Ori"), //da
        new("lam Ori", "gam Ori"), //da 
        new("alf Ori", "zet Ori"), //da
        new("gam Ori", "del Ori"), //da
        new("del Ori", "eps Ori"), //da
        new("eps Ori", "zet Ori"), //da
        new("zet Ori", "kap Ori"), //da
        new("del Ori", "bet Ori"), //da
        new("kap Ori", "bet Ori"), //da
        new("gam Ori", "pi3 Ori"),
        new("pi3 Ori", "pi2 Ori"),
        new("pi2 Ori", "pi1 Ori"),
        new("pi3 Ori", "pi4 Ori"),
        new("pi4 Ori", "pi5 Ori"),
        new("pi5 Ori", "pi6 Ori"),

        // Canis Major
        // === capul ===
        new("the CMa", "iot CMa"),   // theta -> Sirius
        new("iot CMa", "gam CMa"),   // Sirius -> nu2
        new("the CMa", "gam CMa"),   // theta -> Muliphein
        new("iot CMa", "alf CMa"),
        // === partea din față ===
        new("v2 CMa", "alf CMa"),   // Mirzam -> Sirius
        new("v2 CMa", "xi2 CMa"),
        new("v2 CMa", "bet CMa"),
        // === corpul ===
        new("alf CMa", "omi2 CMa"),  // Sirius -> Omicron2
        new("omi2 CMa", "del CMa"),  // Omicron2 -> Wezen
        new("del CMa","sig CMa"),
        new("omi1 CMa","sig CMa"),
        new("omi1 CMa","v2 CMa"),

        // === partea din spate / picioare ===
        new("sig CMa", "eps CMa"),   // Wezen -> Adhara
        new("del CMa", "eta CMa"),   // Wezen -> Aludra
        new("eps CMa", "zet CMa"),   // Adhara -> Furud
        new("eps CMa", "K CMa"), 

        // Canis Minor
        new("alf CMi", "bet CMi"),

        // Gemini (Cei doi gemeni)
        // zona capetelor / partea de sus
        new("alf Gem", "tau Gem"),
        new("tau Gem", "the Gem"),
        new("alf Gem", "tau Gem"),
        // legatura dintre cei doi / zona mainilor
        new("bet Gem", "ups Gem"),
        new("ups Gem", "iot Gem"),
        new("iot Gem", "tau Gem"),
        // partea stanga
        new("kap Gem", "ups Gem"),
        new("ups Gem", "del Gem"),
        new("del Gem", "lam Gem"),
        // corp / picioare
        new("del Gem", "zet Gem"),
        new("zet Gem", "gam Gem"),
        new("lam Gem", "xi Gem"),
        // partea dreapta
        new("tau Gem", "eps Gem"),
        new("eps Gem", "mu Gem"),
        new("eps Gem", "nu Gem"),
        new("mu Gem", "eta Gem"),
        new("eta Gem", "1 Gem"),

        // Taurus 
        new("bet Tau", "eps Tau"),
        new("zet Tau", "alf Tau"),
        new("gam Tau", "lam Tau"),
        new("lam Tau", "xi Tau"),
        new("alf Tau", "the2 Tau"),
        new("the2 Tau", "gam Tau"),
        new("gam Tau", "del Tau"),
        new("eps Tau", "del Tau"),
        
        // Auriga (Poligon)
        // conturul principal
        
        new("bet Aur", "the Aur"),
        new("the Aur", "gam Aur"),
        new("gam Aur", "iot Aur"),
        new("iot Aur", "zet Aur"),
        new("zet Aur", "alf Aur"),
        new("bet Aur","alf Aur"),

        // Leo (Secera și corpul)
        new("eps Leo", "mu Leo"),
        new("mu Leo", "zet Leo"),
        new("zet Leo", "gam Leo"),
        new("gam Leo", "eta Leo"),
        new("eta Leo", "alf Leo"), // Secera terminată
        new("alf Leo", "bet Leo"),
        new("bet Leo", "del Leo"),
        new("del Leo", "gam Leo"),

        // Virgo (Forma clasică Y)
        new("alf Vir", "zet Vir"),
        new("zet Vir", "del Vir"),
        new("gam Vir", "eta Vir"),
        new("gam Vir", "del Vir"),
        new("del Vir", "eps Vir"),
        new("eta Vir", "nu Vir"),
        new("zet Vir", "tau Vir"),
        new("109 Vir", "tau Vir"),
        new("alf Vir", "k Vir"),
        new("iot Vir", "k Vir"),
        new("iot Vir", "mu Vir"),
        new("alf Vir", "gam Vir"),


        // Boötes (Forma de zmeu)
        new("alf Boo", "eps Boo"),
        new("eps Boo", "del Boo"),
        new("del Boo", "bet Boo"),
        new("bet Boo", "gam Boo"),
        new("gam Boo", "rho Boo"),
        new("alf Boo", "eta Boo"),
        new("alf Boo", "rho Boo"),
        new("alf Boo", "zet Boo"),


        // Corona Borealis (Semicercul)
        new("alf CrB", "bet CrB"),
        new("bet CrB", "the CrB"),
        new("alf CrB", "gam CrB"),
        new("gam CrB", "del CrB"),
        new("del CrB","eps CrB"),
        new("eps CrB", "iot CrB"),

        // Hercules
        new("Zeta Herculis", "Epsilon Herculis"),
        new("Epsilon Herculis", "Pi Herculis"),
        new("Pi Herculis", "Eta Herculis"),
        new("Eta Herculis", "Zeta Herculis"),

        // --- THE HEAD & SHOULDERS ---
        new("Zeta Herculis", "Beta Herculis (Kornephoros)"),
        new("Delta Herculis (Sarin)", "Lambda Herculis"),

        // --- THE RIGHT ARM & HAND ---
        new("Beta Herculis (Kornephoros)", "Gamma Herculis"),

       // --- THE LEFT ARM & CLUB ---
        new("Epsilon Herculis", "Lambda Herculis"),
        new("Mu Herculis", "Xi Herculis"),
        new("Mu Herculis", "Lambda Herculis"),
        new("Xi Herculis", "Omicron Herculis"),

        // --- THE UPPER RIGHT LEG ---
        new("Eta Herculis", "Sigma Herculis"),
        new("Sigma Herculis", "Tau Herculis"),
        new("Tau Herculis", "Phi Herculis"),

        // --- THE UPPER LEFT LEG ---
        new("Pi Herculis", "Theta Herculis"),
        new("Theta Herculis", "Iota Herculis"),

        // Lyra
        new("alf Lyr", "eps Lyr"),
        new("alf Lyr", "zet Lyr"),
        new("zet Lyr", "del Lyr"),
        new("del Lyr", "gam Lyr"),
        new("gam Lyr", "bet Lyr"),
        new("bet Lyr", "zet Lyr"),

        // Cygnus (Crucea corectă)
        new("alf Cyg", "gam Cyg"),
        new("gam Cyg", "bet Cyg"), // Trunchiul
        new("del Cyg", "gam Cyg"),
        new("gam Cyg", "eps Cyg"), // Aripile

        // Aquila
        // --- THE BODY / SPINE ---
        new("Gamma Aquilae (Tarazed)", "Alpha Aquilae (Altair)"),
        new("Alpha Aquilae (Altair)", "Beta Aquilae (Alshain)"),
        new("Eta Aquilae", "Delta Aquilae"),
        new("Zeta Aquilae (Okab)", "Delta Aquilae"),
        new("Eta Aquilae", "Theta Aquilae"),
        new("Alpha Aquilae (Altair)", "Delta Aquilae"),


        // --- LEFT WING (North) ---
        new("Zeta Aquilae (Okab)", "Epsilon Aquilae"),

        // --- RIGHT WING (South) ---

        // --- THE TAIL ---
        new("Delta Aquilae", "Lambda Aquilae"),

        // SCORPIUS
        new("Alpha Scorpii", "Sigma Scorpii"),
        new("Alpha Scorpii", "Tau Scorpii"),
        new("Alpha Scorpii", "Delta Scorpii"),
        new("Delta Scorpii", "Beta 1 Scorpii"),
        new("Delta Scorpii", "Pi Scorpii"),
        new("Tau Scorpii", "Epsilon Scorpii"),
        new("Epsilon Scorpii", "Mu 1 Scorpii"),
        new("Mu 1 Scorpii", "Zeta 2 Scorpii"),
        new("Zeta 2 Scorpii", "Eta Scorpii"),
        new("Eta Scorpii", "Theta Scorpii"),
        new("Theta Scorpii", "Iota 1 Scorpii"),
        new("Iota 1 Scorpii", "Kappa Scorpii"),
        new("Kappa Scorpii", "Lambda Scorpii"),
        new("Lambda Scorpii", "Upsilon Scorpii"),

        // SAGITTARIUS (Asterismul "Ceainicul" și structura principală)
        new("Gamma 2 Sagittarii", "Delta Sagittarii"),
        new("Delta Sagittarii", "Epsilon Sagittarii"),
        new("Epsilon Sagittarii", "Gamma 2 Sagittarii"),
        new("Delta Sagittarii", "Lambda Sagittarii"),
        new("Lambda Sagittarii", "Phi Sagittarii"),
        new("Phi Sagittarii", "Delta Sagittarii"),
        new("Phi Sagittarii", "Sigma Sagittarii"),
        new("Sigma Sagittarii", "Tau Sagittarii"),
        new("Tau Sagittarii", "Zeta Sagittarii"),
        new("Zeta Sagittarii", "Sigma Sagittarii"),
        new("Lambda Sagittarii", "Mu Sagittarii"),
        new("Mu Sagittarii", "Xi 2 Sagittarii"),
        new("Xi 2 Sagittarii", "Omicron Sagittarii"),
        new("Omicron Sagittarii", "Pi Sagittarii"),
        new("Pi Sagittarii", "Beta 1 Sagittarii"),
        new("Beta 1 Sagittarii", "Alpha Sagittarii"),
        new("Epsilon Sagittarii", "Zeta Sagittarii"),

        // PEGASUS
        new("Alpha Pegasi", "Beta Pegasi"),
        new("Alpha Pegasi", "Gamma Pegasi"),
        new("Gamma Pegasi", "Alpha Andromedae"),
        new("Beta Pegasi", "Alpha Andromedae"), // Pătratul Pegas (conexiunea directă dintre ambele)
        new("Alpha Pegasi", "Zeta Pegasi"),
        new("Zeta Pegasi", "Theta Pegasi"),
        new("Theta Pegasi", "Epsilon Pegasi"),
        new("Beta Pegasi", "Eta Pegasi"),
        new("Eta Pegasi", "Mu Pegasi"),
        new("Mu Pegasi", "Lambda Pegasi"),
        new("Lambda Pegasi", "Iota Pegasi"),
        new("Iota Pegasi", "Kappa Pegasi"),
        new("Beta Pegasi", "Omicron Pegasi"),
        new("Eta Pegasi", "Pi 2 Pegasi"),
        new("Alpha Pegasi", "Xi Pegasi"),
        new("Xi Pegasi", "Rho Pegasi"),
        new("Rho Pegasi", "Sigma Pegasi"),
        new("Gamma Pegasi", "Phi Pegasi"),
        new("Phi Pegasi", "Chi Pegasi"),

        // ANDROMEDA
        new("Alpha Andromedae", "Delta Andromedae"),
        new("Delta Andromedae", "Beta Andromedae"),
        new("Beta Andromedae", "Gamma 1 Andromedae"),
        new("Beta Andromedae", "Mu Andromedae"),
        new("Mu Andromedae", "Nu Andromedae"),
        new("Delta Andromedae", "Epsilon Andromedae"),
        new("Epsilon Andromedae", "Zeta Andromedae"),
        new("Zeta Andromedae", "Eta Andromedae"),
        new("Alpha Andromedae", "Theta Andromedae"),
        new("Theta Andromedae", "Iota Andromedae"),
        new("Iota Andromedae", "Kappa Andromedae"),
        new("Kappa Andromedae", "Lambda Andromedae"),
        new("Lambda Andromedae", "Omicron Andromedae"),
        new("Gamma 1 Andromedae", "Phi Andromedae"),
        new("Gamma 1 Andromedae", "Upsilon Andromedae"),
        new("Upsilon Andromedae", "Tau Andromedae"),
        new("Tau Andromedae", "Chi Andromedae"),

        // Perseus
        new("alf Per", "gam Per"),
        new("alf Per", "del Per"),
        new("del Per", "eps Per"),
        new("eps Per", "zet Per"),
        new("alf Per", "bet Per"),

        // Triangulum
        new("alf Tri", "bet Tri"),
        new("bet Tri", "gam Tri"),
        new("gam Tri", "alf Tri"),

        // Aries
        new("alf Ari", "bet Ari"),
        new("bet Ari", "gam Ari"),

        // Cetus
        new("Bet Cet", "Iot Cet"),
        new("Iot Cet", "Eta Cet"),
        new("Eta Cet", "The Cet"),
        new("The Cet", "Zet Cet"),
        new("Zet Cet", "Tau Cet"),
        new("Tau Cet", "Bet Cet"),
        new("Zet Cet", "Eps Cet"),
        new("Eps Cet", "Del Cet"),
        new("Del Cet", "Omi Cet"),
        new("Del Cet", "Gam Cet"),
        new("Gam Cet", "Alp Cet"),
        new("Alp Cet", "Lam Cet"),
        new("Lam Cet", "Mu Cet"),
        new("Mu Cet", "Xi 2 Cet"),
        new("Xi 2 Cet", "Nu Cet"),
        new("Nu Cet", "Gam Cet"),
        new("Omi Cet", "Pi Cet"),

        // Hydra
        // === Hydra: cap ===
        new("rho Hya", "eta Hya"),
        new("eta Hya", "sig Hya"),
        new("sig Hya", "del Hya"),
        new("del Hya", "eps Hya"),
        new("eps Hya", "zet Hya"),
        new("zet Hya", "rho Hya"),
        // === Hydra: gat ===
        new("zet Hya", "tet Hya"),
        new("tet Hya", "iot Hya"),
        new("iot Hya", "alf Hya"),
        // === Hydra: corp ===
        new("alf Hya", "ups1 Hya"),
        new("ups1 Hya", "ups2 Hya"),
        new("ups2 Hya", "lam Hya"),
        new("lam Hya", "mu Hya"),
        new("mu Hya", "nu Hya"),
        // === Hydra: coada ===
        new("nu Hya", "xi Hya"),
        new("xi Hya", "bet Hya"),
        new("bet Hya", "gam Hya"),
        new("gam Hya", "pi Hya"),
        new("pi Hya", "58 Hya"),

        // Corvus
        new("alf Crv", "bet Crv"),
        new("bet Crv", "gam Crv"),
        new("gam Crv", "del Crv"),
        new("del Crv", "alf Crv"),

        // Crater
        // === Crater: corpul cupei ===
        new("alf Crt", "bet Crt"),
        new("bet Crt", "gam Crt"),
        new("gam Crt", "del Crt"),
        new("del Crt", "alf Crt"),
        // === Crater: marginea / buza cupei ===
        new("del Crt", "eps Crt"),
        new("eps Crt", "tet Crt"),
        new("gam Crt", "zet Crt"),
        new("zet Crt", "eta Crt"),

       // LIBRA
        new("Alp 2 Lib", "Bet Lib"),
        new("Bet Lib", "Gam Lib"),
        new("Gam Lib", "The Lib"),
        new("Sig Lib", "Gam Lib"),
        new("Sig Lib", "Alp 2 Lib"),

        // CAPRICORNUS
        new("Alp 2 Cap", "Alp 1 Cap"),
        new("Alp 2 Cap", "Bet Cap"),
        new("Alp 1 Cap", "Nu Cap"),
        new("Bet Cap", "Rho Cap"),
        new("Rho Cap", "The Cap"),
        new("The Cap", "Iot Cap"),
        new("Iot Cap", "Gam Cap"),
        new("Gam Cap", "Del Cap"),
        new("Del Cap", "Eps Cap"),
        new("Eps Cap", "Zet Cap"),
        new("Zet Cap", "Omega Cap"),
        new("Omega Cap", "Psi Cap"),
        new("Psi Cap", "Kap Cap"),
        new("Kap Cap", "Bet Cap"),
        new("Del Cap", "Lam Cap"),
        new("Lam Cap", "Mu Cap"),

        // AQUARIUS
        new("Bet Aqr", "Alp Aqr"),
        new("Alp Aqr", "Gam Aqr"),
        new("Gam Aqr", "Zet 1 Aqr"),
        new("Zet 1 Aqr", "Eta Aqr"),
        new("Alp Aqr", "The Aqr"),
        new("The Aqr", "Iot Aqr"),
        new("Lam Aqr", "Psi 1 Aqr"),
        new("Del Aqr", "Tau 2 Aqr"),
        new("Bet Aqr", "Eps Aqr"),
        new("Eta Aqr", "Lam Aqr"),
        new("Psi 1 Aqr", "b1 Aqr"),
        new("The Aqr", "Sigma Aqr"),
        new("Tau 2 Aqr", "Sigma Aqr"),
        new("Tau 2 Aqr", "c2 Aqr"),


        // PISCES (Cordonul de nord, cordonul de sud și peștii)
        new("Alp Psc", "Omi Psc"),
        new("Xi Psc", "Nu Psc"),
        new("Nu Psc", "Mu Psc"),
        new("Mu Psc", "Zet Psc"),
        new("Zet Psc", "Eps Psc"),
        new("Eps Psc", "Del Psc"),
        new("Del Psc", "Omega Psc"),
        new("Omega Psc", "Iot Psc"),
        new("Gam Psc", "The Psc"),
        new("The Psc", "Iot Psc"),
        new("Alp Psc", "Xi Psc"),
        new("Eta Psc", "Rho Psc"),
        new("Eta Psc", "Omi Psc"),
        new("Gam Psc", "Kap Psc"),
        new("Lam Psc", "Kap Psc"),
        new("Lam Psc", "Iot Psc"),
        new("Nu Psc", "Phi Psc"),
        new("Sigma Psc", "Phi Psc"),
        new("Ups Psc", "Phi Psc"),
        new("Ups Psc", "Sigma Psc"),


        // DRACO (Corpul șerpuitor al dragonului și capul)
        new("Gam Dra", "Bet Dra"),
        new("Bet Dra", "Nu 1 Dra"),
        new("Nu 1 Dra", "Xi Dra"),
        new("Xi Dra", "Gam Dra"),
        new("Xi Dra", "Omic Dra"),
        new("Omic Dra", "Del Dra"),
        new("Del Dra", "Eps Dra"),
        new("Eps Dra", "Tau Dra"),
        new("Tau Dra", "Chi Dra"),
        new("Chi Dra", "Zet Dra"),
        new("Zet Dra", "Eta Dra"),
        new("Eta Dra", "The Dra"),
        new("The Dra", "Iot Dra"),
        new("Iot Dra", "Alp Dra"),
        new("Alp Dra", "Kap Dra"),
        new("Kap Dra", "Lam Dra"),
        new("Del Dra", "Xi Dra"),

    };
}
