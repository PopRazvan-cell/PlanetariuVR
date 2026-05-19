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

        StartCoroutine(PollSettings());
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
