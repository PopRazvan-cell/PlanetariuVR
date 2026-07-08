using UnityEngine;
using System.Collections;

/// <summary>
/// Invelis peste motorul TextToSpeech nativ al Android (android.speech.tts.TextToSpeech),
/// folosit pentru a narra textul scenariilor de lectie direct pe casca.
///
/// ATENTIE: Meta Quest ruleaza Horizon OS (Android fara Google Play Services), deci este posibil
/// sa NU existe niciun motor TTS sau nicio voce romaneasca instalata. In acel caz IsAvailable ramane
/// false si lectia continua fara voce (doar cu text). Testeaza pe casca reala.
///
/// In Editor / alte platforme decat Android, totul este no-op.
/// </summary>
public class TextToSpeechManager : MonoBehaviour
{
    [Header("Limba")]
    public string languageCode = "ro";
    public string countryCode = "RO";

    [Header("Motor TTS")]
    // Fortam un motor anume. Pe Quest motorul implicit (com.oculus.systemintelligence) e dezactivat
    // de Meta, iar setarea de sistem tts_default_synth nu se retine — deci il specificam explicit.
    // Gol = foloseste motorul implicit al dispozitivului.
    public string enginePackage = "com.reecedunn.espeak";

    // TextToSpeech.SUCCESS = 0; QUEUE_FLUSH = 0.
    private const int TTS_SUCCESS = 0;
    private const int QUEUE_FLUSH = 0;

    private bool languageOk;
    private bool engineOk;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject tts;
    private InitListener initListener;

    /// <summary>Proxy pentru interfata TextToSpeech.OnInitListener (callback pe thread de binder).</summary>
    private class InitListener : AndroidJavaProxy
    {
        public volatile bool done;
        public volatile int status = -1;
        public InitListener() : base("android.speech.tts.TextToSpeech$OnInitListener") { }
        public void onInit(int status) { this.status = status; done = true; }
    }

    void Start()
    {
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                initListener = new InitListener();
                if (!string.IsNullOrEmpty(enginePackage))
                    // Constructor cu 3 argumente: fortam motorul specificat (ex. eSpeak) pe Quest.
                    tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, initListener, enginePackage);
                else
                    tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, initListener);
            }
            StartCoroutine(WaitForInit());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("TextToSpeechManager: nu am putut initializa TTS - " + e.Message);
        }
    }

    IEnumerator WaitForInit()
    {
        float timeout = 8f;
        while (initListener != null && !initListener.done && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (initListener == null || !initListener.done || initListener.status != TTS_SUCCESS)
        {
            Debug.LogWarning("TextToSpeechManager: niciun motor TTS disponibil pe acest dispozitiv.");
            yield break;
        }

        engineOk = true;

        try
        {
            using (var locale = new AndroidJavaObject("java.util.Locale", languageCode, countryCode))
            {
                int res = tts.Call<int>("setLanguage", locale);
                // >= 0 inseamna disponibila (0/1/2); valori negative = lipsa date / nesuportata.
                languageOk = res >= 0;
                if (!languageOk)
                    Debug.LogWarning($"TextToSpeechManager: limba {languageCode}-{countryCode} nu e disponibila (cod {res}). Narare dezactivata.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("TextToSpeechManager: eroare la setarea limbii - " + e.Message);
        }
    }

    public bool IsAvailable => engineOk && languageOk && tts != null;

    public void Speak(string text)
    {
        if (!IsAvailable || string.IsNullOrEmpty(text)) return;
        try
        {
            // Bundle concret (nu null) ca rezolutia semnaturii JNI sa gaseasca speak(CharSequence,int,Bundle,String).
            using (var pparams = new AndroidJavaObject("android.os.Bundle"))
            {
                tts.Call<int>("speak", text, QUEUE_FLUSH, pparams, "PlanetariuVR-lesson");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("TextToSpeechManager: eroare la speak - " + e.Message);
        }
    }

    public bool IsSpeaking()
    {
        if (tts == null) return false;
        try { return tts.Call<bool>("isSpeaking"); }
        catch { return false; }
    }

    public void StopSpeaking()
    {
        if (tts == null) return;
        try { tts.Call<int>("stop"); }
        catch { /* ignoram */ }
    }

    void OnDestroy()
    {
        if (tts != null)
        {
            try { tts.Call<int>("stop"); tts.Call("shutdown"); }
            catch { /* ignoram */ }
            tts.Dispose();
            tts = null;
        }
    }
#else
    // Editor / non-Android: TTS indisponibil, totul no-op.
    public bool IsAvailable => false;
    public void Speak(string text) { }
    public bool IsSpeaking() => false;
    public void StopSpeaking() { }
#endif
}
