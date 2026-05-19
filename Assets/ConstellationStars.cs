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
    {
        // === Ursa Major (Carul Mare) ===
        new("alf UMa", 165.93f, 61.75f, 1.8f),
        new("bet UMa", 165.46f, 56.38f, 2.4f),
        new("gam UMa", 178.46f, 53.69f, 2.4f),
        new("del UMa", 183.86f, 57.03f, 3.3f),
        new("eps UMa", 193.51f, 55.96f, 1.8f),
        new("zet UMa", 200.98f, 54.93f, 2.1f),
        new("eta UMa", 206.88f, 49.31f, 1.9f),

        // === Ursa Minor (Carul Mic) ===
        new("alf UMi", 37.95f, 89.26f, 2.0f),
        new("bet UMi", 222.68f, 74.15f, 2.1f),
        new("gam UMi", 230.18f, 71.83f, 3.0f),
        new("del UMi", 263.05f, 86.59f, 4.4f),
        new("eps UMi", 251.49f, 82.04f, 4.2f),
        new("zet UMi", 236.01f, 77.79f, 4.3f),
        new("eta UMi", 244.38f, 75.76f, 4.9f),

        // === Cassiopeia ===
        new("alf Cas", 10.13f, 56.54f, 2.2f),
        new("bet Cas", 2.30f, 59.15f, 2.3f),
        new("gam Cas", 14.18f, 60.72f, 2.5f),
        new("del Cas", 21.45f, 60.24f, 2.7f),
        new("eps Cas", 28.60f, 63.67f, 3.4f),

        // === Cepheus ===
        new("alf Cep", 319.64f, 62.59f, 2.4f),
        new("bet Cep", 322.16f, 70.56f, 3.2f),
        new("gam Cep", 354.84f, 77.63f, 3.2f),
        new("del Cep", 337.29f, 58.42f, 4.1f),
        new("eps Cep", 333.76f, 57.04f, 4.2f),
        new("zet Cep", 332.71f, 73.72f, 3.4f),

        // === Orion ===
        new("lam Ori", 83.78f,  9.93f, 3.5f),
        new("alf Ori", 88.79f,  7.41f, 0.5f),
        new("gam Ori", 81.28f,  6.35f, 1.6f),

        new("del Ori", 83.00f, -0.30f, 2.2f),
        new("eps Ori", 84.05f, -1.20f, 1.7f),
        new("zet Ori", 85.19f, -1.94f, 1.8f),

        new("kap Ori", 86.94f, -9.67f, 2.1f),
        new("bet Ori", 78.63f, -8.20f, 0.1f),

        new("pi6 Ori", 75.62f,  1.99f, 4.5f),
        new("pi5 Ori", 74.64f,  2.61f, 3.7f),
        new("pi4 Ori", 73.56f,  5.57f, 3.7f),
        new("pi3 Ori", 72.46f,  6.96f, 3.2f),
        new("pi2 Ori", 72.80f,  8.90f, 4.4f),
        new("pi1 Ori", 72.65f, 10.11f, 4.7f),

        // === Canis Major ===
        new("alf CMa", 101.29f, -16.72f, -1.5f), // Sirius
        new("bet CMa",  95.67f, -17.96f,  2.0f), // Mirzam
        new("gam CMa", 105.94f, -15.63f,  4.1f), // Muliphein
        new("the CMa",  98.76f,  -9.55f,  4.1f), // Theta CMa
        new("nu2 CMa",  99.95f, -18.40f,  3.9f), // Nu2 CMa
        new("omi2 CMa",105.76f, -23.84f,  3.0f), // Omicron2
        new("del CMa", 107.10f, -26.39f,  1.8f), // Wezen
        new("eps CMa", 104.66f, -28.97f,  1.5f), // Adhara
        new("eta CMa", 111.02f, -29.30f,  2.5f), // Aludra
        new("zet CMa",  95.08f, -30.06f,  3.0f), // Furud

        // === Canis Minor ===
        new("alf CMi", 114.83f, 5.22f, 0.4f),
        new("bet CMi", 119.62f, 6.38f, 2.9f),

        // === Gemini ===
        new("alf Gem", 113.65f, 31.89f, 1.58f), // Castor
        new("bet Gem", 116.33f, 28.03f, 1.16f), // Pollux
        new("omi Gem", 114.79f, 34.58f, 4.89f), // Jishui

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
        new("bet Tau", 81.57f, 28.61f, 1.7f),  // Elnath
        new("zet Tau", 84.41f, 21.14f, 3.0f),  // Zeta Tauri
        new("alf Tau", 68.98f, 16.51f, 0.9f),  // Aldebaran
        new("lam Tau", 60.17f, 12.49f, 3.5f),  // Lambda Tauri
        new("xi Tau",  51.79f,  9.73f, 3.7f),  // Xi Tauri
        new("nu Tau",  56.20f,  5.77f, 3.9f),  // Nu Tauri
        new("omi Tau", 51.20f,  9.03f, 3.6f),  // Omicron Tauri
        new("tail",    53.00f, -1.00f, 4.2f),  

        // === Auriga ===
        new("alf Aur", 79.17f, 46.00f, 0.1f),  // Capella
        new("bet Aur", 89.88f, 44.95f, 1.9f),  // Menkalinan
        new("del Aur", 89.88f, 54.28f, 3.7f),  // Delta Aurigae
        new("eps Aur", 75.49f, 43.82f, 3.0f),  // Almaaz
        new("zet Aur", 75.62f, 41.08f, 3.8f),  // Saclateni
        new("eta Aur", 76.63f, 41.23f, 3.2f),  // Haedus
        new("the Aur", 89.93f, 37.21f, 2.7f),  // Mahasim
        new("iot Aur", 74.25f, 33.17f, 2.7f),  // Hassaleh
        new("gam Aur", 81.57f, 28.61f, 1.7f),  // Elnath (istoric Gamma Aur)

        // === Leo ===
        new("alf Leo", 152.09f, 11.97f, 1.4f),
        new("bet Leo", 177.41f, 14.57f, 2.1f), // Corectat (Denebola)
        new("gam Leo", 154.90f, 19.84f, 2.0f),
        new("del Leo", 168.48f, 20.52f, 2.6f),
        new("eps Leo", 146.46f, 23.77f, 3.0f),
        new("zet Leo", 154.16f, 23.42f, 3.4f),
        new("eta Leo", 151.85f, 16.76f, 3.5f),
        new("mu Leo", 148.88f, 26.00f, 3.9f),

        // === Virgo ===
        new("alf Vir", 201.30f, -11.16f, 1.0f),
        new("bet Vir", 177.69f, 1.76f, 3.6f),
        new("gam Vir", 190.44f, -1.45f, 2.7f),
        new("del Vir", 193.92f, 3.42f, 3.4f),
        new("eps Vir", 195.05f, 10.96f, 2.8f), // Corectat (Vindemiatrix)
        new("zet Vir", 203.65f, -0.61f, 3.4f),
        new("eta Vir", 184.97f, -0.67f, 3.9f),

        // === Boötes ===
        new("alf Boo", 213.92f, 19.18f, -0.1f),
        new("bet Boo", 225.62f, 40.39f, 3.5f),
        new("gam Boo", 218.04f, 38.32f, 3.0f),
        new("del Boo", 228.89f, 33.31f, 3.5f),
        new("eps Boo", 221.23f, 27.07f, 2.4f),
        new("zet Boo", 218.52f, 13.73f, 3.8f),
        new("eta Boo", 208.68f, 18.40f, 2.7f),
        new("mu Boo", 231.11f, 37.37f, 4.3f),

        // === Corona Borealis ===
        new("alf CrB", 233.67f, 26.71f, 2.2f),
        new("bet CrB", 231.95f, 29.11f, 3.7f),
        new("gam CrB", 235.68f, 26.29f, 3.8f),
        new("del CrB", 237.40f, 26.07f, 4.6f),
        new("eps CrB", 239.38f, 26.88f, 4.1f),
        new("zet CrB", 234.90f, 36.57f, 5.1f),
        new("eta CrB", 230.83f, 30.28f, 5.0f),

        // === Hercules ===
        new("alf Her", 258.66f, 14.39f, 3.5f),
        new("bet Her", 247.61f, 21.49f, 2.8f),
        new("gam Her", 245.43f, 19.15f, 3.8f),
        new("del Her", 257.77f, 24.84f, 3.1f),
        new("eps Her", 255.33f, 30.93f, 3.9f),
        new("zet Her", 250.22f, 31.60f, 2.8f),
        new("eta Her", 250.78f, 38.92f, 3.5f),

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
        new("zet Cyg", 318.15f, 30.22f, 3.2f),

        // === Aquila ===
        new("alf Aql", 297.70f, 8.87f, 0.8f),
        new("bet Aql", 298.83f, 6.42f, 3.7f),
        new("gam Aql", 296.58f, 10.61f, 2.7f),
        new("del Aql", 289.04f, 3.11f, 3.4f),
        new("eps Aql", 284.15f, 15.07f, 4.0f),
        new("zet Aql", 286.35f, 13.86f, 3.0f),

        // === Scorpius ===
        new("alf Sco", 247.35f, -26.43f, 1.1f),
        new("bet Sco", 241.35f, -19.81f, 2.6f),
        new("del Sco", 239.55f, -22.62f, 2.3f),
        new("eps Sco", 252.53f, -34.29f, 2.3f),
        new("eta Sco", 257.96f, -43.24f, 3.3f),
        new("zet Sco", 253.94f, -42.36f, 3.6f),
        new("mu Sco", 252.93f, -38.04f, 3.0f),
        new("gam Sco", 221.05f, -29.21f, 2.8f),
        new("kap Sco", 264.83f, -39.03f, 2.4f),
        new("iot Sco", 266.86f, -40.13f, 3.0f),

        // === Sagittarius ===
        new("alf Sgr", 288.44f, -40.36f, 3.9f),
        new("bet Sgr", 289.06f, -44.46f, 2.1f),
        new("gam Sgr", 271.46f, -30.42f, 3.0f),
        new("del Sgr", 275.24f, -29.83f, 2.7f),
        new("eps Sgr", 276.04f, -34.38f, 1.8f),
        new("zet Sgr", 285.64f, -29.88f, 2.6f),
        new("eta Sgr", 274.31f, -36.76f, 3.1f),

        // === Pegasus ===
        new("alf Peg", 346.19f, 15.21f, 2.5f),
        new("bet Peg", 345.94f, 28.08f, 2.4f),
        new("gam Peg", 3.31f, 15.18f, 2.8f),
        new("eps Peg", 326.04f, 9.87f, 2.4f),

        // === Andromeda ===
        new("alf And", 2.09f, 29.09f, 2.1f),
        new("bet And", 17.44f, 35.62f, 2.1f),
        new("gam And", 30.97f, 42.33f, 2.3f), // Corectat
        new("del And", 9.83f, 30.86f, 3.3f),

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
        new("alf Cet", 45.57f, 4.09f, 2.5f),
        new("bet Cet", 10.89f, -17.99f, 2.0f),
        new("gam Cet", 38.30f, 3.26f, 3.6f),
        new("del Cet", 39.87f, 0.33f, 4.1f),
        new("mu Cet", 41.10f, 10.15f, 4.3f),
        new("zet Cet", 27.85f, -10.33f, 3.7f),
        new("tau Cet", 26.02f, -15.94f, 3.5f),

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

        // === Libra ===
        new("alf Lib", 222.72f, -16.04f, 2.8f),
        new("bet Lib", 229.25f, -9.38f, 2.6f),
        new("gam Lib", 233.72f, -14.78f, 3.9f),
        new("del Lib", 225.21f, -8.52f, 4.9f),

        // === Capricornus ===
        new("alf Cap", 304.40f, -12.53f, 3.6f),
        new("bet Cap", 305.28f, -14.78f, 3.1f),
        new("gam Cap", 325.04f, -16.66f, 3.7f),
        new("del Cap", 326.77f, -16.13f, 2.9f),
        new("eps Cap", 324.36f, -19.27f, 4.5f),
        new("zet Cap", 321.72f, -22.41f, 3.8f),

        // === Aquarius ===
        new("alf Aqr", 331.42f, -0.32f, 3.0f),
        new("bet Aqr", 322.88f, -5.57f, 2.9f),
        new("gam Aqr", 335.39f, -1.39f, 3.8f),
        new("del Aqr", 340.91f, -15.82f, 3.3f),
        new("eps Aqr", 311.96f, -9.49f, 3.8f),
        new("zet Aqr", 337.21f, -0.01f, 3.6f),

        // === Pisces ===
        new("alf Psc", 30.51f, 2.76f, 3.8f),
        new("bet Psc", 345.96f, 3.82f, 4.5f),
        new("gam Psc", 349.27f, 3.28f, 3.7f),
        new("del Psc", 351.81f, 7.58f, 4.4f),
        new("eps Psc", 15.71f, 7.89f, 4.3f),
        new("zet Psc", 18.45f, 7.58f, 5.2f),

        // === Draco ===
        new("alf Dra", 211.10f, 64.38f, 3.7f),
        new("bet Dra", 263.26f, 52.30f, 2.8f),
        new("gam Dra", 269.15f, 51.49f, 2.2f),
        new("del Dra", 288.08f, 67.66f, 3.1f),
        new("eps Dra", 297.04f, 70.27f, 3.8f),
        new("zet Dra", 256.49f, 65.71f, 3.2f),
        new("eta Dra", 245.99f, 61.51f, 2.7f),
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
        new("gam Cep", "zet Cep"),
        new("zet Cep", "alf Cep"),
        new("alf Cep", "eta Cep"), // Dacă eta e adăugată ulterior
        new("del Cep", "eps Cep"),

        // Orion (Corpul și Centura)
        // === Orion ===
        new("lam Ori", "alf Ori"),
        new("lam Ori", "gam Ori"),
        new("alf Ori", "gam Ori"),
        new("alf Ori", "zet Ori"),
        new("gam Ori", "del Ori"),
        new("del Ori", "eps Ori"),
        new("eps Ori", "zet Ori"),
        new("zet Ori", "kap Ori"),
        new("del Ori", "bet Ori"),
        new("kap Ori", "bet Ori"),
        new("eps Ori", "tet Ori"),
        new("tet Ori", "iot Ori"),
        new("gam Ori", "pi3 Ori"),
        new("pi3 Ori", "pi2 Ori"),
        new("pi2 Ori", "pi1 Ori"),
        new("pi3 Ori", "pi4 Ori"),
        new("pi4 Ori", "pi5 Ori"),
        new("pi5 Ori", "pi6 Ori"),

        // Canis Major
        // === capul ===
        new("the CMa", "alf CMa"),   // theta -> Sirius
        new("alf CMa", "nu2 CMa"),   // Sirius -> nu2
        new("the CMa", "gam CMa"),   // theta -> Muliphein
        // === partea din față ===
        new("bet CMa", "alf CMa"),   // Mirzam -> Sirius
        // === corpul ===
        new("alf CMa", "omi2 CMa"),  // Sirius -> Omicron2
        new("omi2 CMa", "del CMa"),  // Omicron2 -> Wezen
        // === partea din spate / picioare ===
        new("del CMa", "eps CMa"),   // Wezen -> Adhara
        new("del CMa", "eta CMa"),   // Wezen -> Aludra
        new("eps CMa", "zet CMa"),   // Adhara -> Furud

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
        new("bet Tau", "alf Tau"),
        new("zet Tau", "alf Tau"),
        new("alf Tau", "lam Tau"),
        new("lam Tau", "xi Tau"),
        new("nu Tau", "xi Tau"),
        new("xi Tau", "omi Tau"),
        new("omi Tau", "tail"),
        
        // Auriga (Poligon)
        // conturul principal
        new("del Aur", "bet Aur"),
        new("bet Aur", "the Aur"),
        new("the Aur", "gam Aur"),
        new("gam Aur", "iot Aur"),
        new("iot Aur", "alf Aur"),
        new("alf Aur", "del Aur"),
        // bara din mijloc
        new("bet Aur", "alf Aur"),
        // grupul mic din dreapta ("The Kids")
        new("alf Aur", "eps Aur"),
        new("eps Aur", "zet Aur"),
        new("zet Aur", "eta Aur"),
        new("eta Aur", "alf Aur"),

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
        new("zet Vir", "gam Vir"),
        new("gam Vir", "eta Vir"),
        new("gam Vir", "del Vir"),
        new("del Vir", "eps Vir"),
        new("eta Vir", "bet Vir"),

        // Boötes (Forma de zmeu)
        new("alf Boo", "eps Boo"),
        new("eps Boo", "del Boo"),
        new("del Boo", "bet Boo"),
        new("bet Boo", "gam Boo"),
        new("gam Boo", "alf Boo"),
        new("alf Boo", "eta Boo"),

        // Corona Borealis (Semicercul)
        new("alf CrB", "bet CrB"),
        new("bet CrB", "del CrB"),
        new("alf CrB", "gam CrB"),
        new("gam CrB", "eps CrB"),
        new("eps CrB", "zet CrB"),

        // Hercules
        new("alf Her", "bet Her"),
        new("bet Her", "gam Her"),
        new("gam Her", "del Her"),
        new("del Her", "eps Her"),
        new("eps Her", "zet Her"),
        new("zet Her", "eta Her"),

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
        new("alf Aql", "bet Aql"),
        new("alf Aql", "gam Aql"),
        new("alf Aql", "del Aql"),
        new("del Aql", "eps Aql"),
        new("del Aql", "zet Aql"),

        // Scorpius (Cârligul)
        new("alf Sco", "bet Sco"),
        new("bet Sco", "del Sco"),
        new("alf Sco", "tau Sco"), // (Dacă adaugi tau ulterior, altfel leagă de eps)
        new("alf Sco", "eps Sco"),
        new("eps Sco", "mu Sco"),
        new("mu Sco", "zet Sco"),
        new("zet Sco", "eta Sco"),

        // Sagittarius (Ceainicul)
        new("gam Sgr", "del Sgr"),
        new("del Sgr", "eps Sgr"),
        new("eps Sgr", "zet Sgr"),
        new("zet Sgr", "gam Sgr"), // Corpul ceainicului
        new("del Sgr", "eta Sgr"),
        new("zet Sgr", "alf Sgr"),

        // Pegasus (Pătratul Mare)
        new("alf Peg", "bet Peg"),
        new("bet Peg", "gam Peg"),
        // a 4-a stea e alf And (Sirrah), pe care o ai la Andromeda
        new("gam Peg", "alf Peg"),
        new("eps Peg", "alf Peg"),

        // Andromeda
        new("alf And", "del And"),
        new("del And", "bet And"),
        new("bet And", "gam And"),

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
        new("alf Cet", "gam Cet"),
        new("gam Cet", "del Cet"),
        new("del Cet", "mu Cet"),
        new("mu Cet", "zet Cet"),
        new("zet Cet", "tau Cet"),

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

        // Libra
        new("alf Lib", "bet Lib"),
        new("bet Lib", "gam Lib"),
        new("gam Lib", "del Lib"),
        new("del Lib", "alf Lib"),

        // Capricornus
        new("alf Cap", "bet Cap"),
        new("bet Cap", "gam Cap"),
        new("gam Cap", "del Cap"),
        new("del Cap", "eps Cap"),
        new("eps Cap", "zet Cap"),

        // Aquarius
        new("alf Aqr", "bet Aqr"),
        new("bet Aqr", "gam Aqr"),
        new("gam Aqr", "del Aqr"),
        new("del Aqr", "eps Aqr"),
        new("eps Aqr", "zet Aqr"),

        // Pisces
        new("alf Psc", "bet Psc"),
        new("bet Psc", "gam Psc"),
        new("gam Psc", "del Psc"),
        new("del Psc", "eps Psc"),
        new("eps Psc", "zet Psc"),

        // Draco
        new("alf Dra", "bet Dra"),
        new("bet Dra", "gam Dra"),
        new("gam Dra", "del Dra"),
        new("del Dra", "eps Dra"),
        new("eps Dra", "zet Dra"),
        new("zet Dra", "eta Dra")
    };
}
