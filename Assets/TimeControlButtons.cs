using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System;

public class TimeControlButtons : MonoBehaviour
{
    [Header("References")]
    public PlanetariumManager planetariumManager;
    public string leftHandAnchorName = "LeftHandOnControllerAnchor";

    [Header("Button Layout")]
    public Vector3 buttonSize = new Vector3(0.06f, 0.025f, 0.005f);
    
    [Header("Clock Alignment")]
    public string existingClockName = "CeasDataOraManaStanga";
    public float clockToButtonOffset = 0.06f; 
    public float buttonSpacing = 0.04f;       

    [Header("Button Colors")]
    public Color buttonBgColor = new Color(0.2f, 0.2f, 0.3f, 0.85f);
    public Color buttonHighlightColor = new Color(0.3f, 0.5f, 0.9f, 0.95f);
    public Color buttonTextColor = Color.white;
    public Color activeButtonBgColor = new Color(0.15f, 0.6f, 0.25f, 0.9f);

    [Header("Interaction (Laser)")]
    [Tooltip("Trage aici obiectul care are scriptul AstroLaser (sau lasă gol dacă e în scenă)")]
    public AstroLaser astroLaser;
    
    [Tooltip("Lungimea razei de interacțiune")]
    public float interactionRange = 100f; // Laserul e lung, lăsăm o rază mare

    private Transform anchorTransform;
    private Transform clockReference;
    private List<TimeButton> timeButtons = new List<TimeButton>();
    private float lastInteractionTime;
    private int activeButtonIndex = 0;

    private class TimeButton
    {
        public GameObject buttonObject;
        public MeshRenderer meshRenderer;
        public TextMeshPro textMesh;
        public float timeMultiplier;
        public int index;
        public Material originalMaterial;
    }

    void Start()
    {
        anchorTransform = FindAnchor();
        if (anchorTransform == null) { Debug.LogError("Lipsă Left Hand Anchor!"); return; }
        if (planetariumManager == null) { Debug.LogError("Lipsă PlanetariumManager!"); return; }

        // Căutare automată AstroLaser dacă nu e setat manual
        if (astroLaser == null)
        {
            astroLaser = FindObjectOfType<AstroLaser>();
            if (astroLaser == null) Debug.LogWarning("Nu am găsit AstroLaser în scenă automat.");
        }

        // 1. ALINIERE CEAS ȘI BUTOANE
        Transform foundClock = anchorTransform.Find(existingClockName);
        if (foundClock != null) clockReference = foundClock;

        var configs = new[] { 
            new { Label="1x", Val=1f }, new { Label="2x", Val=2f }, 
            new { Label="5x", Val=5f }, new { Label="10x", Val=10f } 
        };
        
        for (int i = 0; i < configs.Length; i++) CreateButton(configs[i].Label, configs[i].Val, i);
        UpdateActiveButton(0);
    }

    void Update()
    {
        if (timeButtons.Count == 0) return;

        bool hitDetected = false;

        // Verificăm dacă avem un laser activ și o mână funcțională
        if (astroLaser != null && astroLaser.isLaserActive && astroLaser.rightHand != null && astroLaser.rightHand.IsTracked)
        {
            Transform pointer = astroLaser.rightHand.PointerPose;
            
            if (pointer != null)
            {
                Ray laserRay = new Ray(pointer.position, pointer.forward);
                RaycastHit hit;

                // Verificăm dacă raza laserului lovește vreun buton
                if (Physics.Raycast(laserRay, out hit, interactionRange))
                {
                    foreach (var btn in timeButtons)
                    {
                        if (hit.collider.gameObject == btn.buttonObject)
                        {
                            hitDetected = true;
                            
                            // Highlight vizual (Albastru)
                            btn.originalMaterial.color = buttonHighlightColor;
                            
                            // Activare buton (Click)
                            if (Time.time >= lastInteractionTime + 0.2f) // Cooldown de 0.2s
                            {
                                UpdateActiveButton(btn.index);
                                lastInteractionTime = Time.time;
                            }
                            break;
                        }
                    }
                }
            }
        }

        // Resetăm culorile butoanelor care NU sunt atinse de laser acum
        if (!hitDetected)
        {
            foreach (var btn in timeButtons)
            {
                if (btn.index == activeButtonIndex)
                {
                    // Păstrăm culoarea activă (Verde)
                    btn.originalMaterial.color = activeButtonBgColor;
                    btn.textMesh.color = Color.white;
                }
                else
                {
                    // Culoare normală (Gri/Albastru închis)
                    btn.originalMaterial.color = buttonBgColor;
                    btn.textMesh.color = new Color(0.7f, 0.7f, 0.7f);
                }
            }
        }
    }

    void CreateButton(string label, float multiplier, int index)
    {
        GameObject btnObj = new GameObject("TimeBtn_" + label);
        Transform parentToUse = clockReference != null ? clockReference : anchorTransform;
        
        // Calculăm poziția: orizontal sub ceas
        float totalWidth = (3 * (buttonSize.x + buttonSpacing));
        float startX = -totalWidth / 2f + (buttonSize.x/2);
        float xPos = startX + index * (buttonSize.x + buttonSpacing);
        
        btnObj.transform.SetParent(parentToUse, false);
        btnObj.transform.localPosition = new Vector3(xPos, -clockToButtonOffset, 0f);
        btnObj.transform.localRotation = clockReference != null ? Quaternion.identity : Quaternion.Euler(65, 0, 0);

        MeshFilter mf = btnObj.AddComponent<MeshFilter>();
        mf.mesh = CreateBoxMesh(buttonSize.x, buttonSize.y, buttonSize.z);
        MeshRenderer mr = btnObj.AddComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Standard")) { color = buttonBgColor };

        // Collider necesar pentru Raycast
        BoxCollider bc = btnObj.AddComponent<BoxCollider>();
        bc.size = buttonSize;

        TextMeshPro tm = btnObj.AddComponent<TextMeshPro>();
        tm.text = label; tm.alignment = TextAlignmentOptions.Center; tm.fontSize = 24;
        tm.transform.localScale = Vector3.one * 0.003f;

        timeButtons.Add(new TimeButton { buttonObject = btnObj, meshRenderer = mr, textMesh = tm, timeMultiplier = multiplier, index = index, originalMaterial = mr.material });
    }

    void UpdateActiveButton(int index)
    {
        activeButtonIndex = index;
        for (int i = 0; i < timeButtons.Count; i++)
        {
            bool isActive = (i == index);
            timeButtons[i].originalMaterial.color = isActive ? activeButtonBgColor : buttonBgColor;
            timeButtons[i].textMesh.color = isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f);
        }
        planetariumManager.SetTimeMultiplier(timeButtons[index].timeMultiplier);
        Debug.Log("Viteză timp: " + timeButtons[index].timeMultiplier + "x");
    }

    Mesh CreateBoxMesh(float x, float y, float z) {
        GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tmp.transform.localScale = new Vector3(x,y,z);
        Mesh m = tmp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tmp); return m;
    }

    Transform FindAnchor()
    {
        GameObject a = GameObject.Find(leftHandAnchorName);
        if (a != null) return a.transform;
        a = GameObject.Find("LeftHandAnchor");
        return a != null ? a.transform : null;
    }
}