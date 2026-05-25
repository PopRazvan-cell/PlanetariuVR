using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using System.IO;
using System.Collections;

public class IntroVideoPlayer : MonoBehaviour
{
    [Header("Video Settings")]
    public string videoUrl = "https://api.exoplanethunter.binarysquad.club/api/assets/exohunt.mp4";
    public string localFileName = "intro.mp4";

    [Header("Screen Settings")]
    public float screenDistance = 10f;
    public float screenWidth = 9.6f;
    public float screenHeight = 6.48f; // 9.6 * (2160/3200) — aspect ratio 40:27
    public float screenVerticalOffset = 2f;

    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;
    private GameObject screenObject;
    private AudioSource audioSource;
    private PlanetariumManager manager;

    void Start()
    {
        Camera cam = Camera.main;
        if (cam == null) { Finish(); return; }

        manager = FindObjectOfType<PlanetariumManager>();
        CreateVideoScreen(cam);
        StartCoroutine(PlayWhenReady());
    }

    void CreateVideoScreen(Camera cam)
    {
        screenObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        screenObject.name = "IntroVideoScreen";
        Destroy(screenObject.GetComponent<Collider>());

        // Ecran static in world space — NU urmeaza camera
        Vector3 worldPos = cam.transform.position + cam.transform.forward * screenDistance + Vector3.up * screenVerticalOffset;
        screenObject.transform.position = worldPos;
        screenObject.transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
        screenObject.transform.localScale = new Vector3(screenWidth, screenHeight, 1f);

        renderTexture = new RenderTexture(3200, 2160, 0, RenderTextureFormat.ARGB32);
        renderTexture.useMipMap = false;
        renderTexture.antiAliasing = 1;
        renderTexture.Create();

        var rend = screenObject.GetComponent<Renderer>();
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;

        Shader shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
        if (shader != null)
            rend.material = new Material(shader);

        rend.material.mainTexture = renderTexture;
    }

    IEnumerator PlayWhenReady()
    {
        string localPath = Path.Combine(Application.persistentDataPath, localFileName);
        string prefKey = "intro_video_version_" + localFileName;
        bool needsDownload = !File.Exists(localPath);

        // Verifica daca serverul are o versiune noua.
        // Serverul nu accepta HEAD, dar accepta GET cu Range: bytes=0-0 (descarca 1 byte).
        if (!needsDownload)
        {
            using (UnityWebRequest check = UnityWebRequest.Get(videoUrl))
            {
                check.SetRequestHeader("User-Agent", "PlanetariuVR/1.0");
                check.SetRequestHeader("Range", "bytes=0-0");
                check.timeout = 10;
                yield return check.SendWebRequest();

                if (check.result == UnityWebRequest.Result.Success ||
                    check.responseCode == 206)
                {
                    string serverVersion = check.GetResponseHeader("ETag")
                                       ?? check.GetResponseHeader("Last-Modified");
                    string localVersion = PlayerPrefs.GetString(prefKey, "");

                    if (serverVersion != null && serverVersion != localVersion)
                    {
                        Debug.Log($"IntroVideoPlayer: versiune noua detectata ({serverVersion}), re-download...");
                        File.Delete(localPath);
                        needsDownload = true;
                    }
                }
                else
                {
                    Debug.LogWarning("IntroVideoPlayer: check versiune esuat (" + check.error + "), folosesc fisierul local.");
                }
            }
        }

        if (needsDownload)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(videoUrl))
            {
                request.SetRequestHeader("User-Agent", "PlanetariuVR/1.0");
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 60;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogWarning("IntroVideoPlayer: download esuat, streaming: " + request.error);
                    PlayVideo(videoUrl);
                    yield break;
                }

                File.WriteAllBytes(localPath, request.downloadHandler.data);

                // Salveaza versiunea curenta pentru comparatii viitoare
                string savedVersion = request.GetResponseHeader("ETag")
                                   ?? request.GetResponseHeader("Last-Modified")
                                   ?? "";
                if (savedVersion.Length > 0)
                {
                    PlayerPrefs.SetString(prefKey, savedVersion);
                    PlayerPrefs.Save();
                }

                Debug.Log("IntroVideoPlayer: salvat in " + localPath);
            }
        }

        PlayVideo("file://" + localPath);
    }

    void PlayVideo(string sourceUrl)
    {
        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.url = sourceUrl;
        videoPlayer.isLooping = false;
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.controlledAudioTrackCount = 1;
        videoPlayer.EnableAudioTrack(0, true);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.volume = 1f;
        videoPlayer.SetTargetAudioSource(0, audioSource);
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.Prepare();
    }

    void OnVideoPrepared(VideoPlayer source) => source.Play();

    void OnVideoFinished(VideoPlayer source) => Finish();

    void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError("IntroVideoPlayer error: " + message);
        Finish();
    }

    void Finish()
    {
        Cleanup();
        if (manager != null)
            manager.OnIntroVideoFinished();
    }

    void Cleanup()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            Destroy(videoPlayer);
        }
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
        if (screenObject != null)
            Destroy(screenObject);
    }

    void OnDestroy() => Cleanup();
}
