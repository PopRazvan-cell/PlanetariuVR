using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

public class ApiSettingsFetcher : MonoBehaviour
{
    public PlanetariumManager planetariumManager;
    public ConstellationRenderer constellationRenderer;
    public ConstellationStars constellationStars;
    public LessonManager lessonManager;
    public string apiUrl = "https://api.exoplanethunter.binarysquad.club/api/setari";
    public float pollInterval = 1f;

    [Serializable]
    public class DateConfigurare
    {
        public string oras;
        public string latitudine;
        public string longitudine;
        public int viteza;
        public string foloseste_data_curenta;
        public string data_si_ora_obs;
        public string afisare_constelatii;
        public int lectie_activa; // ID-ul lectiei active; 0 = nicio lectie
    }

    [Serializable]
    public class SettingsApiResponse
    {
        public string status;
        public DateConfigurare date_configurare;
    }

    private string previousLatitudine;
    private string previousLongitudine;
    private int previousViteza;
    private string previousFolosesteDataCurenta;
    private string previousDataSiOraObs;
    private string previousAfisareConstelatii;
    private int previousLectieActiva = -1; // -1 = neinitializat (0 e o valoare valida: "fara lectie")

    void Start()
    {
        if (planetariumManager == null)
            planetariumManager = FindObjectOfType<PlanetariumManager>();

        if (planetariumManager == null)
        {
            Debug.LogError("ApiSettingsFetcher: PlanetariumManager nu a fost găsit!");
            return;
        }

        if (constellationRenderer == null)
            constellationRenderer = FindObjectOfType<ConstellationRenderer>();
        if (constellationStars == null)
            constellationStars = FindObjectOfType<ConstellationStars>();
        if (lessonManager == null)
            lessonManager = FindObjectOfType<LessonManager>();

        StartCoroutine(PollSettings());
    }

    /// <summary>
    /// Forteaza re-aplicarea tuturor setarilor live la urmatorul poll (reseteaza cache-ul de comparatie).
    /// Apelata de LessonManager dupa terminarea unei lectii, ca sa readuca configuratia live.
    /// </summary>
    public void ForceReapply()
    {
        previousLatitudine = null;
        previousLongitudine = null;
        previousViteza = int.MinValue;
        previousFolosesteDataCurenta = null;
        previousDataSiOraObs = null;
        previousAfisareConstelatii = null;
    }

    IEnumerator PollSettings()
    {
        while (true)
        {
            yield return StartCoroutine(FetchAndApply());
            yield return new WaitForSeconds(pollInterval);
        }
    }

    IEnumerator FetchAndApply()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogWarning("ApiSettingsFetcher: Eroare la conectare - " + request.error);
                yield break;
            }

            string json = request.downloadHandler.text;
            SettingsApiResponse response = JsonUtility.FromJson<SettingsApiResponse>(json);

            if (response == null || response.date_configurare == null)
            {
                Debug.LogWarning("ApiSettingsFetcher: JSON invalid");
                yield break;
            }

            DateConfigurare cfg = response.date_configurare;

            // --- Lectii: detectam schimbarea id-ului lectiei active si notificam LessonManager ---
            if (lessonManager != null && cfg.lectie_activa != previousLectieActiva)
            {
                previousLectieActiva = cfg.lectie_activa;
                lessonManager.OnLessonIdChanged(cfg.lectie_activa);
            }

            // Cat timp ruleaza o lectie, scenariul controleaza cerul (locatie/timp/constelatii/stele),
            // asa ca NU aplicam setarile live din /api/setari ca sa nu se bata cap in cap.
            if (lessonManager != null && lessonManager.IsLessonActive)
                yield break;

            bool somethingChanged = false;

            if (cfg.latitudine != previousLatitudine)
            {
                previousLatitudine = cfg.latitudine;
                if (float.TryParse(cfg.latitudine, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float lat))
                    planetariumManager.latitude = lat;
                somethingChanged = true;
            }

            if (cfg.longitudine != previousLongitudine)
            {
                previousLongitudine = cfg.longitudine;
                if (float.TryParse(cfg.longitudine, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float lon))
                    planetariumManager.longitude = lon;
                somethingChanged = true;
            }

            bool useCurrent = cfg.foloseste_data_curenta != null && cfg.foloseste_data_curenta.Trim().ToLower() == "da";

            if (previousFolosesteDataCurenta != cfg.foloseste_data_curenta || previousViteza != cfg.viteza)
            {
                previousFolosesteDataCurenta = cfg.foloseste_data_curenta;
                previousViteza = cfg.viteza;

                planetariumManager.useRealTime = useCurrent;
                planetariumManager.currentTimeMultiplier = useCurrent ? 1f : cfg.viteza;
                somethingChanged = true;
            }

            if (!useCurrent && previousDataSiOraObs != cfg.data_si_ora_obs)
            {
                previousDataSiOraObs = cfg.data_si_ora_obs;

                if (!string.IsNullOrEmpty(cfg.data_si_ora_obs))
                {
                    if (System.DateTime.TryParse(cfg.data_si_ora_obs, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out System.DateTime parsedTime))
                    {
                        planetariumManager.SetCurrentDateTime(parsedTime);
                    }
                }
                somethingChanged = true;
            }

            if (cfg.afisare_constelatii != previousAfisareConstelatii)
            {
                previousAfisareConstelatii = cfg.afisare_constelatii;
                bool show = cfg.afisare_constelatii != null && cfg.afisare_constelatii.Trim().ToLower() == "da";

                if (constellationRenderer != null)
                    constellationRenderer.SetConstellationsVisible(show);
                if (constellationStars != null)
                    constellationStars.SetConstellationsVisible(show);

                somethingChanged = true;
            }

            if (somethingChanged)
                Debug.Log("ApiSettingsFetcher: Setări actualizate de la API");
        }
    }
}
