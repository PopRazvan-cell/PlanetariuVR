using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

/// <summary>
/// Gestionează logica aplicației Unity: comunicarea cu API-ul,
/// instanțierea obiectelor 3D și actualizarea cadrelor (Update) folosind datele din AstroMath.
/// </summary>
public class PlanetariumManager : MonoBehaviour
{
    [Header("Setări Vizuale")]
    public GameObject starPrefab;
    public float radius = 1000f; // Distanța stelelor față de observator

    [Header("Puncte Cardinale")]
    public bool showCardinalPoints = true;
    public float cardinalDistance = 150f;
    public float cardinalHeight = 8f;
    public float cardinalFontSize = 18f;
    public Color cardinalColor = new Color(0.45f, 0.9f, 1f, 1f);

    [Header("Ceas Mână Stângă")]
    public bool showLeftHandClock = true;
    public string leftHandAnchorName = "LeftHandOnControllerAnchor";
    public Vector3 leftHandClockLocalPosition = new Vector3(0f, 0.015f, 0.02f);
    public Vector3 leftHandClockLocalRotation = new Vector3(65f, 0f, 0f);
    public float leftHandClockFontSize = 0.08f;
    public Color leftHandClockColor = Color.white;

    [Header("Muzică")]
    public bool playMusicOnSceneStart = true;
    public string musicManagerName = "MusicManager";
    
    [Header("Setări Timp")]
    public bool useRealTime = true;
    [HideInInspector] public float simulatedTimeMultiplier = 1f;

    [HideInInspector] public float currentTimeMultiplier = 1f;

    public event Action OnStarsLoaded;

    [Header("Locație Observator (Autocompletat de API)")]
    public float latitude = 0f;
    public float longitude = 0f;

    // --- STRUCTURI PENTRU DESERIALIZARE JSON ---
    [Serializable]
    public class StarItem
    {
        public int ID;
        public string TIC_ID;
        public string name;
        public string ra;
        public string declination; 
        public string description;
    }

    [Serializable]
    public class ApiResponse
    {
        public float latitudine;
        public float longitudine;
        public int total_gasite;
        public StarItem[] date; 
    }
    // -------------------------------------------

    // Clasă pentru a reține informațiile fiecărei stele din mediul 3D
    public class StarInstance
    {
        public string name;
        public GameObject starObject;
        public float ra;
        public float dec;
    }

    private List<StarInstance> allStars = new List<StarInstance>();
    public IReadOnlyList<StarInstance> Stars => allStars;
    private List<Transform> cardinalLabels = new List<Transform>();
    private Transform leftHandClockAnchor;
    private TextMeshPro leftHandClockText;
    private float nextClockRefreshTime;
    private DateTime currentTime;
    private string apiUrl = "https://api.exoplanethunter.binarysquad.club/api/stele/obseravtorVR";

    void Start()
    {
        currentTime = DateTime.UtcNow;
        PlaySceneMusic();
        CreateCardinalPoints();
        CreateLeftHandClock();
        StartCoroutine(FetchStarsFromAPI());
    }

    IEnumerator FetchStarsFromAPI()
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(apiUrl))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Eroare la conectarea API: " + webRequest.error);
            }
            else
            {
                string jsonResponse = webRequest.downloadHandler.text;
                ApiResponse parsedData = JsonUtility.FromJson<ApiResponse>(jsonResponse);

                if (parsedData != null && parsedData.date != null)
                {
                    // Preia locația din API
                    latitude = parsedData.latitudine;
                    longitude = parsedData.longitudine;
                    
                    GenerateStars(parsedData.date);
                    OnStarsLoaded?.Invoke();
                }
            }
        }
    }

    void GenerateStars(StarItem[] starDataArray)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        foreach (StarItem item in starDataArray)
        {
            try
            {
                // Curățăm datele textuale primite din API (eliminăm simbolul de grad)
                string cleanRa = item.ra.Replace("°", "").Trim();
                string cleanDec = item.declination.Replace("°", "").Trim();
                
                float raVal = float.Parse(cleanRa, culture);
                float decVal = float.Parse(cleanDec, culture);

                // Extragem magnitudinea vizuală (Vmag) pentru a calcula strălucirea
                float magVal = 2.5f; 
                if (!string.IsNullOrEmpty(item.name) && item.name.Contains("Vmag:"))
                {
                    string magString = item.name.Replace("Stea Vmag:", "").Trim();
                    float.TryParse(magString, System.Globalization.NumberStyles.Float, culture, out magVal);
                }

                // Generăm obiectul 3D în Unity
                GameObject newStar = Instantiate(starPrefab);
                newStar.name = item.TIC_ID; 
                newStar.AddComponent<LookAtCamera>(); 
                newStar.AddComponent<StarTwinkle>(); // Decomentează dacă ai creat scriptul de pâlpâire

                StarData sd = newStar.GetComponent<StarData>();

                if (sd != null)
                {
                    sd.starName = !string.IsNullOrEmpty(item.name) ? item.name : "Stea";
                    sd.TIC_ID = item.TIC_ID;
                    sd.ra = "RA: " + item.ra;
                    sd.dec = "DEC: " + item.declination;
                    sd.desc = item.description;
                }

                // Calculăm o scală aparentă adecvată pentru vizualizarea în VR la distanțe mari
                float baseScale = radius * 0.0015f; 
                float magnitudeFactor = Mathf.Clamp(5f - magVal, 0.5f, 4f);
                float starScale = baseScale * magnitudeFactor; 
                
                newStar.transform.localScale = new Vector3(starScale, starScale, starScale);

                // Adăugăm steaua în lista de actualizare
                StarInstance starInfo = new StarInstance();
                starInfo.name = item.name;
                starInfo.starObject = newStar;
                starInfo.ra = raVal;
                starInfo.dec = decVal;
                
                allStars.Add(starInfo);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Eroare la procesarea stelei " + item.TIC_ID + ": " + e.Message);
            }
        }
    }

    void Update()
    {
        UpdateCardinalLabelsRotation();
        UpdateLeftHandClock();

        // Oprim rularea dacă datele nu au fost încă descărcate
        if (allStars.Count == 0) return;

        // 1. Gestiunea Timpului
        if (useRealTime && currentTimeMultiplier <= 1f)
        {
            currentTime = DateTime.UtcNow;
        }
        else
        {
            currentTime = currentTime.AddSeconds(Time.deltaTime * currentTimeMultiplier);
        }

        // 2. Delegăm calculele fundamentale de timp către AstroMath
        double jd = AstroMath.CalculateJulianDate(currentTime);
        double lst = AstroMath.CalculateLocalSiderealTime(jd, longitude);

        // 3. Actualizăm poziția fiecărei stele pe baza noului Timp Sideral Local
        foreach (StarInstance star in allStars)
        {
            // Calculăm coordonatele orizontale pure
            AstroMath.EquatorialToHorizontal(star.ra, star.dec, latitude, lst, out float altRad, out float azRad);
            
            // Le convertim în spațiul 3D din Unity
            Vector3 newPosition = AstroMath.SphericalToCartesian(altRad, azRad, radius);
            
            // Aplicăm transformarea
            star.starObject.transform.position = newPosition;
        }
    }

    void CreateCardinalPoints()
    {
        if (!showCardinalPoints) return;

        CreateCardinalLabel("N", new Vector3(0f, cardinalHeight, cardinalDistance));
        CreateCardinalLabel("E", new Vector3(cardinalDistance, cardinalHeight, 0f));
        CreateCardinalLabel("S", new Vector3(0f, cardinalHeight, -cardinalDistance));
        CreateCardinalLabel("V", new Vector3(-cardinalDistance, cardinalHeight, 0f));
    }

    void CreateCardinalLabel(string label, Vector3 position)
    {
        GameObject labelObject = new GameObject("PunctCardinal_" + label);
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = position;

        TextMeshPro text = labelObject.AddComponent<TextMeshPro>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = cardinalFontSize;
        text.color = cardinalColor;
        text.enableWordWrapping = false;
        text.rectTransform.sizeDelta = new Vector2(20f, 10f);

        cardinalLabels.Add(labelObject.transform);
    }

    void UpdateCardinalLabelsRotation()
    {
        if (cardinalLabels.Count == 0 || Camera.main == null) return;

        Transform cameraTransform = Camera.main.transform;

        foreach (Transform label in cardinalLabels)
        {
            if (label == null) continue;

            label.LookAt(cameraTransform);
            label.Rotate(0f, 180f, 0f);
        }
    }

    void CreateLeftHandClock()
    {
        if (!showLeftHandClock) return;

        leftHandClockAnchor = FindLeftHandClockAnchor();
        if (leftHandClockAnchor == null) return;

        GameObject clockObject = new GameObject("CeasDataOraManaStanga");
        clockObject.transform.SetParent(leftHandClockAnchor, false);
        clockObject.transform.localPosition = leftHandClockLocalPosition;
        clockObject.transform.localRotation = Quaternion.Euler(leftHandClockLocalRotation);

        leftHandClockText = clockObject.AddComponent<TextMeshPro>();
        leftHandClockText.alignment = TextAlignmentOptions.Center;
        leftHandClockText.fontSize = leftHandClockFontSize;
        leftHandClockText.color = leftHandClockColor;
        leftHandClockText.enableWordWrapping = false;
        leftHandClockText.rectTransform.sizeDelta = new Vector2(1.2f, 0.35f);

        RefreshLeftHandClockText();
    }

    Transform FindLeftHandClockAnchor()
    {
        GameObject anchorObject = GameObject.Find(leftHandAnchorName);
        if (anchorObject != null) return anchorObject.transform;

        anchorObject = GameObject.Find("LeftControllerAnchor");
        if (anchorObject != null) return anchorObject.transform;

        anchorObject = GameObject.Find("LeftHandAnchor");
        return anchorObject != null ? anchorObject.transform : null;
    }

    void UpdateLeftHandClock()
    {
        if (!showLeftHandClock) return;

        if (leftHandClockText == null)
        {
            CreateLeftHandClock();
            return;
        }

        if (Time.time >= nextClockRefreshTime)
        {
            RefreshLeftHandClockText();
        }
    }

    void RefreshLeftHandClockText()
    {
        if (leftHandClockText == null) return;

        leftHandClockText.text =
            currentTime.ToString("dd.MM.yyyy\nHH:mm:ss") +
            $"\nLat: {latitude:F4}" +
            $"\nLong: {longitude:F4}";
        nextClockRefreshTime = Time.time + 1f;
    }

    void PlaySceneMusic()
    {
        if (!playMusicOnSceneStart) return;

        GameObject musicManager = GameObject.Find(musicManagerName);
        if (musicManager == null) return;

        AudioSource audioSource = musicManager.GetComponent<AudioSource>();
        if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    public void SetTimeMultiplier(float multiplier)
    {
        currentTimeMultiplier = multiplier;
        useRealTime = (multiplier <= 1f);
    }

    public void SetCurrentDateTime(DateTime dateTime)
    {
        currentTime = dateTime;
    }
}
