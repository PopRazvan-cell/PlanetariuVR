using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

/// <summary>
/// Gestioneaza "lectiile astro": secvente de scenarii aduse din API.
/// Cand /api/setari raporteaza un `lectie_activa` != 0, aduce lectia de la /api/lectii/{id},
/// apoi ruleaza scenariile pe rand: aplica setarile scenariului, incarca stelele lui de la
/// /api/stele/{scenariu_id}, afiseaza textul si asteapta `durata` secunde. La final restaureaza cerul live.
/// </summary>
public class LessonManager : MonoBehaviour
{
    [Header("Referinte")]
    public PlanetariumManager planetariumManager;
    public ConstellationRenderer constellationRenderer;
    public ConstellationStars constellationStars;
    public ApiSettingsFetcher settingsFetcher;
    public TextToSpeechManager speech;

    [Header("API")]
    public string lectiiApiBase = "https://api.exoplanethunter.binarysquad.club/api/lectii/";
    public string steleApiBase = "https://api.exoplanethunter.binarysquad.club/api/stele/";

    [Header("Panou Text Lectie (pe mana stanga)")]
    // Ancorat la mana stanga, ca ceasul din PlanetariumManager — nu acopera cerul.
    public string leftHandAnchorName = "LeftHandOnControllerAnchor";
    // Pozitionat putin mai sus decat ceasul (ceasul e la y=0.015), ca sa nu se suprapuna.
    public Vector3 panelLocalPosition = new Vector3(0f, 0.06f, 0.02f);
    public Vector3 panelLocalRotation = new Vector3(65f, 0f, 0f);
    public float panelFontSize = 0.08f;
    public Vector2 panelSize = new Vector2(1.2f, 0.6f);
    public Color panelTextColor = Color.white;

    private int activeLessonId = 0;
    private Coroutine lessonRoutine;
    private GameObject captionObject;
    private TextMeshPro captionText;

    public bool IsLessonActive => activeLessonId != 0;

    // ─────────────── Structuri deserializare JSON ───────────────

    [Serializable]
    public class LectieResponse
    {
        public string status;
        public LectieData date;
    }

    [Serializable]
    public class LectieData
    {
        public int id;
        public string nume;
        public string descriere;
        public Scenariu[] scenarii;
    }

    [Serializable]
    public class Scenariu
    {
        public int id;
        public string nume;
        public int viteza;
        public int bortle;
        public float latitudine;
        public float longitudine;
        public string data_si_ora_obs;
        public string foloseste_data_curenta;
        public string afisare_constelatii;
        public string text;
        public int durata; // secunde
    }

    [Serializable]
    public class SteleScenariuResponse
    {
        public int bortle_level;
        public int total_gasite;
        public PlanetariumManager.StarItem[] date;
    }

    // ─────────────── Ciclu de viata ───────────────

    void Start()
    {
        if (planetariumManager == null)
            planetariumManager = FindObjectOfType<PlanetariumManager>();
        if (constellationRenderer == null)
            constellationRenderer = FindObjectOfType<ConstellationRenderer>();
        if (constellationStars == null)
            constellationStars = FindObjectOfType<ConstellationStars>();
        if (settingsFetcher == null)
            settingsFetcher = FindObjectOfType<ApiSettingsFetcher>();
        if (speech == null)
            speech = FindObjectOfType<TextToSpeechManager>();
    }

    // Panoul e ancorat de mana stanga (parent), deci se misca odata cu mana — nu are nevoie de Update.

    // ─────────────── Punct de intrare (apelat de ApiSettingsFetcher) ───────────────

    /// <summary>
    /// Apelata cand /api/setari raporteaza un nou `lectie_activa`.
    /// 0 = nicio lectie (opreste si restaureaza cerul live).
    /// </summary>
    public void OnLessonIdChanged(int newId)
    {
        if (newId == activeLessonId) return;

        if (lessonRoutine != null)
        {
            StopCoroutine(lessonRoutine);
            lessonRoutine = null;
            if (speech != null) speech.StopSpeaking();
        }

        if (newId != 0)
        {
            // Ramane activa neintrerupt (fara restaurare intre lectii) — scenariul nou reconfigureaza tot.
            activeLessonId = newId;
            lessonRoutine = StartCoroutine(RunLesson(newId));
        }
        else
        {
            activeLessonId = 0;
            HideCaption();
            RestoreAfterLesson();
        }
    }

    // ─────────────── Rularea lectiei ───────────────

    IEnumerator RunLesson(int lessonId)
    {
        LectieData lectie = null;
        yield return StartCoroutine(FetchLesson(lessonId, r => lectie = r));

        if (lectie == null || lectie.scenarii == null || lectie.scenarii.Length == 0)
        {
            Debug.LogWarning($"LessonManager: lectia {lessonId} nu are scenarii valide.");
            activeLessonId = 0;
            RestoreAfterLesson();
            yield break;
        }

        Debug.Log($"LessonManager: pornesc lectia '{lectie.nume}' cu {lectie.scenarii.Length} scenarii.");

        // Durata totala a lectiei (pentru cronometrul lectiei) si cat s-a scurs inainte de scenariul curent.
        float lessonTotal = 0f;
        foreach (Scenariu s in lectie.scenarii) lessonTotal += Mathf.Max(1f, s.durata);
        float lessonElapsedBefore = 0f;

        for (int idx = 0; idx < lectie.scenarii.Length; idx++)
        {
            Scenariu sc = lectie.scenarii[idx];
            // Daca id-ul activ s-a schimbat intre timp, iesim (coroutina veche a fost inlocuita).
            if (activeLessonId != lessonId) yield break;

            ApplyScenarioSettings(sc);
            // /api/stele/{X} primeste NIVELUL BORTLE (1-9), nu id-ul scenariului.
            yield return StartCoroutine(LoadScenarioStars(Mathf.Clamp(sc.bortle, 1, 9)));

            // Pornim naratiunea (daca exista voce pe dispozitiv).
            bool narrating = speech != null && speech.IsAvailable && !string.IsNullOrEmpty(sc.text);
            if (narrating)
                speech.Speak(sc.text);

            // Asteptam max(durata, lungimea naratiunii): trecem mai departe doar cand a expirat
            // `durata` SI naratiunea s-a terminat (nu taiem vocea).
            float elapsed = 0f;
            float duration = Mathf.Max(1f, sc.durata);
            float warmup = 0f; // scurt ragaz ca isSpeaking() sa devina true dupa Speak()
            int lastShownSecond = -1;
            while (true)
            {
                if (activeLessonId != lessonId)
                {
                    if (narrating) speech.StopSpeaking();
                    yield break;
                }

                elapsed += Time.deltaTime;
                warmup += Time.deltaTime;

                // Cronometru live: actualizam panoul doar cand se schimba secunda afisata.
                float scenarioRemaining = Mathf.Max(0f, duration - elapsed);
                float lessonRemaining = Mathf.Max(0f, lessonTotal - (lessonElapsedBefore + Mathf.Min(elapsed, duration)));
                int shownSecond = Mathf.CeilToInt(scenarioRemaining);
                if (shownSecond != lastShownSecond)
                {
                    lastShownSecond = shownSecond;
                    ShowCaption(lectie.nume, sc, idx + 1, lectie.scenarii.Length, scenarioRemaining, lessonRemaining);
                }

                bool durataDone = elapsed >= duration;
                bool voiceDone = !narrating || (warmup > 0.5f && !speech.IsSpeaking());
                if (durataDone && voiceDone)
                    break;

                yield return null;
            }

            lessonElapsedBefore += duration;
        }

        // Lectia s-a terminat natural.
        Debug.Log($"LessonManager: lectia '{lectie.nume}' s-a terminat.");
        if (speech != null) speech.StopSpeaking();
        activeLessonId = 0;
        HideCaption();
        RestoreAfterLesson();
    }

    void ApplyScenarioSettings(Scenariu sc)
    {
        if (planetariumManager != null)
        {
            planetariumManager.latitude = sc.latitudine;
            planetariumManager.longitude = sc.longitudine;

            bool useCurrent = !string.IsNullOrEmpty(sc.foloseste_data_curenta) &&
                              sc.foloseste_data_curenta.Trim().ToLower() == "da";

            planetariumManager.useRealTime = useCurrent;
            planetariumManager.currentTimeMultiplier = useCurrent ? 1f : sc.viteza;

            if (!useCurrent && !string.IsNullOrEmpty(sc.data_si_ora_obs) &&
                DateTime.TryParse(sc.data_si_ora_obs, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                planetariumManager.SetCurrentDateTime(parsed);
            }
        }

        bool show = !string.IsNullOrEmpty(sc.afisare_constelatii) &&
                    sc.afisare_constelatii.Trim().ToLower() == "da";
        if (constellationRenderer != null)
            constellationRenderer.SetConstellationsVisible(show);
        if (constellationStars != null)
            constellationStars.SetConstellationsVisible(show);
    }

    IEnumerator LoadScenarioStars(int bortleLevel)
    {
        string url = steleApiBase + bortleLevel;
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogWarning($"LessonManager: eroare la /api/stele/{bortleLevel}: {request.error}");
                yield break;
            }

            SteleScenariuResponse resp = JsonUtility.FromJson<SteleScenariuResponse>(request.downloadHandler.text);
            if (resp != null && resp.date != null && planetariumManager != null)
                planetariumManager.LoadStars(resp.date);
        }
    }

    IEnumerator FetchLesson(int lessonId, Action<LectieData> callback)
    {
        string url = lectiiApiBase + lessonId;
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogWarning($"LessonManager: eroare la /api/lectii/{lessonId}: {request.error}");
                callback(null);
                yield break;
            }

            LectieResponse resp = JsonUtility.FromJson<LectieResponse>(request.downloadHandler.text);
            callback(resp != null ? resp.date : null);
        }
    }

    void RestoreAfterLesson()
    {
        // Readuce stelele observatorului si forteaza re-aplicarea setarilor live de la /api/setari.
        if (planetariumManager != null)
            planetariumManager.RestoreObserverStars();
        if (settingsFetcher != null)
            settingsFetcher.ForceReapply();
    }

    // ─────────────── Panou text ───────────────

    void EnsureCaption()
    {
        if (captionObject != null) return;

        captionObject = new GameObject("LectieCaption");

        Transform anchor = FindLeftHandAnchor();
        if (anchor != null)
        {
            captionObject.transform.SetParent(anchor, false);
            captionObject.transform.localPosition = panelLocalPosition;
            captionObject.transform.localRotation = Quaternion.Euler(panelLocalRotation);
        }
        else
        {
            Debug.LogWarning("LessonManager: nu am gasit ancora mainii stangi; panoul lectiei nu are parinte.");
        }

        captionText = captionObject.AddComponent<TextMeshPro>();
        captionText.alignment = TextAlignmentOptions.Center;
        captionText.fontSize = panelFontSize;
        captionText.color = panelTextColor;
        captionText.enableWordWrapping = true;
        captionText.rectTransform.sizeDelta = panelSize;
        captionObject.SetActive(false);
    }

    Transform FindLeftHandAnchor()
    {
        GameObject anchor = GameObject.Find(leftHandAnchorName);
        if (anchor != null) return anchor.transform;

        anchor = GameObject.Find("LeftControllerAnchor");
        if (anchor != null) return anchor.transform;

        anchor = GameObject.Find("LeftHandAnchor");
        return anchor != null ? anchor.transform : null;
    }

    void ShowCaption(string lessonName, Scenariu sc, int scenarioIndex, int scenarioCount,
                     float scenarioRemaining, float lessonRemaining)
    {
        EnsureCaption();
        string scenarioName = sc.nume != null ? sc.nume.Trim() : "";
        string scenarioText = sc.text != null ? sc.text.Trim() : "";
        captionText.text =
            $"<b>{lessonName}</b>\n" +
            $"<size=70%><color=#7FDBFF>Scenariu {scenarioIndex}/{scenarioCount}</color>   " +
            $"<color=#FFD166>{FormatTime(scenarioRemaining)}</color></size>\n" +
            $"<size=80%><color=#7FDBFF>{scenarioName}</color></size>\n" +
            $"<size=70%>{scenarioText}</size>\n" +
            $"<size=55%><color=#9AA0A6>Lectie: {FormatTime(lessonRemaining)} ramas</color></size>";
        captionObject.SetActive(true);
    }

    static string FormatTime(float seconds)
    {
        int t = Mathf.CeilToInt(seconds);
        return $"{t / 60}:{(t % 60):00}";
    }

    void HideCaption()
    {
        if (captionObject != null)
            captionObject.SetActive(false);
    }
}
