using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class AstroLaser : MonoBehaviour
{
    [Header("Referințe Mână și Laser")]
    public OVRHand rightHand; 
    public LineRenderer laserLine;
    public float laserLength = 5000f;
    [Header("Stabilizare")]
    public float smoothingSpeed = 15f;
    private Vector3 currentSmoothedEndPoint;
    public bool isLaserActive = true;
    [HideInInspector] public bool suppressPinchAction = false; // dezactiveaza trimiterea TIC in timpul quiz-ului
    public Color laserColor = new Color(0f, 1f, 0.2f, 1f);
    public float laserStartWidth = 0.015f;
    public float laserEndWidth = 0.004f;

    [Header("Referințe Panou Info")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Transform playerCamera;
    public Vector3 infoPanelCameraOffset = new Vector3(0f, -0.15f, 1.2f);

    [Header("Persistență Panou")]
    public float infoPanelDwellTime = 3f;
    private float lastStarHitTime = -999f;

    [Header("Pinch pentru trimitere TIC_ID")]
    public OVRHand.HandFinger pinchFinger = OVRHand.HandFinger.Index;
    public float pinchCooldown = 1f;
    public string steaCurentaApiUrl = "https://api.exoplanethunter.binarysquad.club/api/stea_curenta";
    public AudioSource audioSource;
    public float beepFrequency = 880f;
    public float beepDuration = 0.15f;

    private bool wasPinching;
    private float lastPinchTime;
    private string currentTicId;
    private AudioClip beepClip;

    private Image panelBackground;

    void Start()
    {
        ConfigureLaserVisuals();
        SetupPanelBackground();
        SetupAudio();
    }

    void SetupAudio()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        beepClip = CreateBeepClip(beepFrequency, beepDuration);
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    AudioClip CreateBeepClip(float frequency, float duration)
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * duration);
        AudioClip clip = AudioClip.Create("Beep", samples, 1, sampleRate, false);
        float[] wave = new float[samples];
        for (int i = 0; i < samples; i++)
            wave[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate);
        clip.SetData(wave, 0);
        return clip;
    }

    void SetupPanelBackground()
    {
        if (infoPanel == null) return;

        panelBackground = infoPanel.GetComponentInChildren<Image>();
        if (panelBackground == null)
        {
            GameObject bgObj = new GameObject("PanelBackground");
            bgObj.transform.SetParent(infoPanel.transform, false);
            bgObj.transform.SetAsFirstSibling();

            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            panelBackground = bgObj.AddComponent<Image>();
            panelBackground.color = new Color(0f, 0f, 0f, 0.75f);
        }
        else
        {
            panelBackground.color = new Color(0f, 0f, 0f, 0.75f);
        }
    }

    void Update()
    {
        if (!isLaserActive)
        {
            HideLaserVisuals();
            return;
        }

        if (rightHand == null)
        {
            return;
        }

        if (!rightHand.IsTracked)
        {
            HideLaserVisuals();
            return;
        }
        
        Transform pointer = rightHand.PointerPose;
        if (pointer != null)
        {
            if (laserLine != null)
            {
                laserLine.enabled = true;
                laserLine.SetPosition(0, pointer.position);
            }

            Vector3 direction = pointer.forward;
            Ray ray = new Ray(pointer.position, direction);

            RaycastHit[] hits = Physics.RaycastAll(ray, laserLength);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool amLovitStea = false;
            Vector3 targetEndPoint = pointer.position + (direction * laserLength);

            foreach (RaycastHit hit in hits)
            {
                StarData data = hit.collider.GetComponent<StarData>();
                
                if (data != null)
                {
                    targetEndPoint = hit.point;
                    currentTicId = data.TIC_ID;
                    lastStarHitTime = Time.time;
                    ShowFloatingPanel(data, hit.point);
                    amLovitStea = true;
                    break; 
                }
            }

            currentSmoothedEndPoint = Vector3.Lerp(currentSmoothedEndPoint, targetEndPoint, Time.deltaTime * smoothingSpeed);
            
            if (laserLine != null)
            {
                laserLine.SetPosition(1, currentSmoothedEndPoint);
            }

            if (!amLovitStea)
            {
                currentTicId = null;
                if (Time.time - lastStarHitTime > infoPanelDwellTime)
                {
                    if (infoPanel != null) infoPanel.SetActive(false);
                }
            }

            bool isPinching = rightHand.GetFingerIsPinching(pinchFinger);
            if (!suppressPinchAction && isPinching && !wasPinching && Time.time >= lastPinchTime + pinchCooldown)
            {
                if (!string.IsNullOrEmpty(currentTicId))
                {
                    StartCoroutine(SendTicIdToApi(currentTicId));
                    lastPinchTime = Time.time;
                }
            }
            wasPinching = isPinching;
        }
        else
        {
            HideLaserVisuals();
        }
    }

    public void SetLaserActive(bool active)
    {
        isLaserActive = active;

        if (!isLaserActive)
        {
            HideLaserVisuals();
        }
    }

    public void ToggleLaser()
    {
        SetLaserActive(!isLaserActive);
    }

    void HideLaserVisuals()
    {
        if (laserLine != null)
        {
            laserLine.enabled = false;
        }

        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }
    }

    void ConfigureLaserVisuals()
    {
        if (laserLine == null) return;

        laserLine.startColor = laserColor;
        laserLine.endColor = laserColor;
        laserLine.startWidth = laserStartWidth;
        laserLine.endWidth = laserEndWidth;
        laserLine.positionCount = 2;
        laserLine.useWorldSpace = true;

        if (laserLine.material == null)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                laserLine.material = new Material(shader);
            }
        }

        if (laserLine.material != null)
        {
            laserLine.material.color = laserColor;
        }
    }

    IEnumerator SendTicIdToApi(string ticId)
    {
        string json = $"{{\"TIC_ID\": \"{ticId}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(steaCurentaApiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogWarning($"AstroLaser: Eroare trimitere TIC_ID {ticId}: {request.error}");
            }
            else
            {
                Debug.Log($"AstroLaser: TIC_ID {ticId} trimis cu succes");
                if (audioSource != null && beepClip != null)
                    audioSource.PlayOneShot(beepClip);
            }
        }
    }

    void ShowFloatingPanel(StarData data, Vector3 hitPoint)
    {
        Transform cameraTransform = GetInfoPanelCameraTransform();

        if (infoPanel != null && infoText != null && cameraTransform != null)
        {
            infoPanel.SetActive(true);

            RectTransform panelRect = infoPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.sizeDelta = new Vector2(500f, 180f);
            }

            infoText.enableAutoSizing = false;
            infoText.fontSize = 18f;

            infoText.text =
                $"<size=95%><b>{data.starName}</b></size>\n" +
                $"<size=50%><color=#7FDBFF>TIC ID</color>  {data.TIC_ID}</size>\n" +
                $"<size=70%><color=#7FDBFF>────────────</color></size>\n" +
                $"<size=75%><color=#7FDBFF>Ascensiune Dreapta</color>  {data.ra}\n" +
                $"<color=#7FDBFF>Declinație</color>  {data.dec}</size>\n" +
                $"<size=50%><color=#7FDBFF></color>  {data.desc}</size>";

            Vector3 fixedPosition =
                cameraTransform.position +
                cameraTransform.right * infoPanelCameraOffset.x +
                cameraTransform.up * infoPanelCameraOffset.y +
                cameraTransform.forward * infoPanelCameraOffset.z;

            infoPanel.transform.position = fixedPosition;
            infoPanel.transform.LookAt(cameraTransform);
            infoPanel.transform.Rotate(0, 180, 0);
        }
    }

    Transform GetInfoPanelCameraTransform()
    {
        GameObject centerEye = GameObject.Find("CenterEyeAnchor");
        if (centerEye != null) return centerEye.transform;

        if (Camera.main != null) return Camera.main.transform;

        return playerCamera;
    }
}
