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
    // Audio narat generat pe server: /api/assets/{id_scenariu}.wav
    public string assetsApiBase = "https://api.exoplanethunter.binarysquad.club/api/assets/";

    [Header("Naratiune")]
    // Preferam audio de la server (voce naturala); daca lipseste, cadem pe TTS on-device.
    public bool preferServerAudio = true;
    [Range(0f, 1f)] public float narrationVolume = 1f;

    [Header("Panou Text Lectie (pe mana stanga)")]
    // Ancorat la mana stanga, ca ceasul din PlanetariumManager — nu acopera cerul.
    public string leftHandAnchorName = "LeftHandOnControllerAnchor";
    // Pozitionat putin mai sus decat ceasul (ceasul e la y=0.015), ca sa nu se suprapuna.
    public Vector3 panelLocalPosition = new Vector3(0f, 0.06f, 0.02f);
    public Vector3 panelLocalRotation = new Vector3(65f, 0f, 0f);
    public float panelFontSize = 0.08f;
    public Vector2 panelSize = new Vector2(1.2f, 0.6f);
    public Color panelTextColor = Color.white;
    // Wrap manual garantat: rupem textul scenariului la atatea caractere pe linie (pe cuvinte intregi).
    public int wrapCharsPerLine = 30;

    private int activeLessonId = 0;
    private Coroutine lessonRoutine;
    private GameObject captionObject;
    private TextMeshPro captionText;
    private AudioSource narrationAudio;

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

        // AudioSource pentru naratiunea de la server (2D, mereu audibil).
        narrationAudio = gameObject.AddComponent<AudioSource>();
        narrationAudio.playOnAwake = false;
        narrationAudio.spatialBlend = 0f;
        narrationAudio.volume = narrationVolume;
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
            StopNarration();
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

            // Naratiune: preferam audio de la server (/api/assets/{id}.wav); daca lipseste, TTS on-device.
            AudioClip clip = null;
            if (preferServerAudio)
                yield return StartCoroutine(DownloadScenarioAudio(sc.id, c => clip = c));

            bool usingServerAudio = clip != null && narrationAudio != null;
            bool narratingTTS = false;
            if (usingServerAudio)
            {
                narrationAudio.clip = clip;
                narrationAudio.volume = narrationVolume;
                narrationAudio.Play();
            }
            else
            {
                narratingTTS = speech != null && speech.IsAvailable && !string.IsNullOrEmpty(sc.text);
                if (narratingTTS)
                    speech.Speak(sc.text);
            }

            // Asteptam max(durata, lungimea naratiunii): trecem mai departe doar cand a expirat
            // `durata` SI naratiunea s-a terminat (nu taiem vocea).
            float elapsed = 0f;
            float duration = Mathf.Max(1f, sc.durata);
            float warmup = 0f; // scurt ragaz ca isPlaying/isSpeaking sa devina true dupa start
            int lastShownSecond = -1;
            while (true)
            {
                if (activeLessonId != lessonId)
                {
                    StopNarration();
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
                bool voiceDone;
                if (usingServerAudio)
                    voiceDone = warmup > 0.3f && !narrationAudio.isPlaying;
                else
                    voiceDone = !narratingTTS || (warmup > 0.5f && !speech.IsSpeaking());
                if (durataDone && voiceDone)
                    break;

                yield return null;
            }

            lessonElapsedBefore += duration;
        }

        // Lectia s-a terminat natural.
        Debug.Log($"LessonManager: lectia '{lectie.nume}' s-a terminat.");
        StopNarration();
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

    /// <summary>
    /// Descarca fisierul audio narat al scenariului de la /api/assets/{id}.wav.
    /// Intoarce null daca lipseste sau esueaza (atunci se foloseste TTS on-device ca fallback).
    /// </summary>
    IEnumerator DownloadScenarioAudio(int scenarioId, Action<AudioClip> callback)
    {
        string url = assetsApiBase + scenarioId + ".wav";
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogWarning($"LessonManager: fara audio pentru scenariul {scenarioId} ({request.error}); folosesc TTS on-device.");
                callback(null);
                yield break;
            }

            callback(DownloadHandlerAudioClip.GetContent(request));
        }
    }

    /// <summary>Opreste orice naratiune in curs (audio de la server + TTS on-device).</summary>
    void StopNarration()
    {
        if (narrationAudio != null && narrationAudio.isPlaying)
            narrationAudio.Stop();
        if (speech != null)
            speech.StopSpeaking();
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

        // Fixam ancorele/pivotul la centru, ca sizeDelta sa fie latimea reala a casetei.
        RectTransform rt = captionText.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = panelSize;

        // Cheia wrap-ului: containerul NU trebuie sa se auto-lateasca dupa text,
        // altfel textul ramane pe o singura linie foarte lunga.
        captionText.autoSizeTextContainer = false;
        captionText.enableAutoSizing = false;
        captionText.enableWordWrapping = true;
        captionText.overflowMode = TextOverflowModes.Overflow; // creste pe verticala, nu taie
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
        // Reaplicam configuratia de wrap la fiecare afisare (uneori nu se aplica daca e setata
        // o singura data inainte de generarea textului).
        captionText.autoSizeTextContainer = false;
        captionText.enableWordWrapping = true;
        captionText.overflowMode = TextOverflowModes.Overflow;
        captionText.rectTransform.sizeDelta = panelSize;
        string scenarioName = sc.nume != null ? sc.nume.Trim() : "";
        string scenarioText = WrapText(sc.text != null ? sc.text.Trim() : "", wrapCharsPerLine);
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

    /// <summary>
    /// Rupe manual textul la maxChars caractere pe linie, pe cuvinte intregi, pastrand
    /// eventualele newline-uri existente. Garanteaza wrap independent de comportamentul TMP.
    /// </summary>
    static string WrapText(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || maxChars <= 0) return text;

        var sb = new System.Text.StringBuilder();
        string[] paragraphs = text.Split('\n');
        for (int p = 0; p < paragraphs.Length; p++)
        {
            if (p > 0) sb.Append('\n');
            int lineLen = 0;
            foreach (string word in paragraphs[p].Split(' '))
            {
                if (word.Length == 0) continue;
                if (lineLen > 0 && lineLen + 1 + word.Length > maxChars)
                {
                    sb.Append('\n');
                    lineLen = 0;
                }
                else if (lineLen > 0)
                {
                    sb.Append(' ');
                    lineLen++;
                }
                sb.Append(word);
                lineLen += word.Length;
            }
        }
        return sb.ToString();
    }

    void HideCaption()
    {
        if (captionObject != null)
            captionObject.SetActive(false);
    }
}
