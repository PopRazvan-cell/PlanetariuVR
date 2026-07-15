using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

/// <summary>
/// Quiz auto-generat din stelele afisate in scenariul curent (fara API).
/// Foloseste doar stelele stralucitoare (magnitudine mica, aprox. bortle 8).
/// Tipuri de intrebari:
///  - alegere multipla (culoare / constelatie / cea mai stralucitoare / emisfera)
///  - "arata o stea" cu laserul + pinch (stea dintr-o constelatie / cea mai stralucitoare)
/// Ofera feedback (corect/gresit + explicatie) si scor.
/// </summary>
public class QuizManager : MonoBehaviour
{
    [Header("Referinte")]
    public OVRHand rightHand;              // aceeasi mana ca AstroLaser
    public AstroLaser astroLaser;          // il suprimam in timpul quiz-ului
    public TextToSpeechManager speech;     // optional, pentru narare
    public PlanetariumManager planetariumManager; // pentru latitudine (emisfera)

    [Header("Setari")]
    public int questionsPerQuiz = 5;
    // Doar stelele mai stralucitoare de aceasta magnitudine intra in quiz (~bortle 8 = cele mai luminoase).
    public float maxMagnitude = 4.0f;
    public OVRHand.HandFinger pinchFinger = OVRHand.HandFinger.Index;
    public float pinchCooldown = 0.6f;
    public float feedbackSeconds = 2.5f;
    public float raycastDistance = 6000f;  // stelele sunt departe (raza ~1000)

    [Header("Asezare (in fata camerei)")]
    public float distance = 2.5f;
    public float questionHeight = 0.34f;
    public float firstOptionOffset = 0.06f;
    public float optionSpacing = 0.20f;
    public Vector2 questionSize = new Vector2(1.1f, 0.28f);
    public Vector2 optionSize = new Vector2(1.0f, 0.16f);
    // Folosite ca font MAXIM; textul se auto-dimensioneaza sa umple panoul.
    public float questionFontSize = 0.11f;
    public float optionFontSize = 0.11f;

    [Header("Culori")]
    public Color panelColor = new Color(0f, 0f, 0f, 0.85f);
    public Color optionColor = new Color(0.10f, 0.15f, 0.30f, 0.92f);
    public Color hoverColor = new Color(0.20f, 0.35f, 0.60f, 0.95f);
    public Color correctColor = new Color(0.15f, 0.55f, 0.20f, 0.97f);
    public Color wrongColor = new Color(0.70f, 0.15f, 0.15f, 0.97f);

    private bool quizActive;
    private float lastPinchTime;

    private class StarInfo
    {
        public StarData data;
        public float mag;
        public Dictionary<string, string> fields;
    }

    private class Question
    {
        public bool isPoint;                       // true = "arata o stea" (laser+pinch pe stea)
        public string text;
        public string explanation;
        // alegere multipla:
        public string[] options;
        public int correct;
        // "arata o stea":
        public System.Func<StarData, bool> isCorrectStar;
    }

    // ─────────────── API public ───────────────

    public void StartQuiz()
    {
        if (!quizActive) StartCoroutine(RunQuiz());
    }

    public IEnumerator RunQuiz()
    {
        if (quizActive) yield break;
        quizActive = true;

        if (planetariumManager == null) planetariumManager = FindObjectOfType<PlanetariumManager>();

        List<Question> questions = BuildQuestions(GatherStars(), questionsPerQuiz);
        if (questions.Count == 0)
        {
            Debug.LogWarning("QuizManager: nu am putut genera intrebari (prea putine stele luminoase).");
            quizActive = false;
            yield break;
        }

        if (astroLaser != null) astroLaser.suppressPinchAction = true;

        int score = 0;
        for (int i = 0; i < questions.Count; i++)
        {
            bool ok = false;
            if (questions[i].isPoint)
                yield return StartCoroutine(AskPointQuestion(questions[i], i + 1, questions.Count, r => ok = r));
            else
                yield return StartCoroutine(AskChoiceQuestion(questions[i], i + 1, questions.Count, r => ok = r));
            if (ok) score++;
        }

        yield return StartCoroutine(ShowResult(score, questions.Count));

        if (astroLaser != null) astroLaser.suppressPinchAction = false;
        quizActive = false;
    }

    // ─────────────── Adunare stele + parsare ───────────────

    List<StarInfo> GatherStars()
    {
        var list = new List<StarInfo>();
        foreach (StarData sd in FindObjectsOfType<StarData>())
        {
            if (sd == null || !sd.gameObject.activeInHierarchy) continue;
            if (string.IsNullOrEmpty(sd.starName) || sd.starName.StartsWith("Stea")) continue;
            if (string.IsNullOrEmpty(sd.desc)) continue;

            var fields = ParseDesc(sd.desc);
            float mag = 99f;
            if (fields.TryGetValue("Magnitudine", out string mstr)) mag = ParseLeadingFloat(mstr, 99f);
            if (mag > maxMagnitude) continue; // doar stelele stralucitoare (bortle 8)

            list.Add(new StarInfo { data = sd, mag = mag, fields = fields });
        }
        return list;
    }

    Dictionary<string, string> ParseDesc(string desc)
    {
        var d = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(desc)) return d;
        foreach (string line in desc.Split('\n'))
        {
            int c = line.IndexOf(':');
            if (c > 0)
            {
                string k = line.Substring(0, c).Trim();
                string v = line.Substring(c + 1).Trim();
                if (!string.IsNullOrEmpty(v) && !d.ContainsKey(k)) d[k] = v;
            }
        }
        return d;
    }

    float ParseLeadingFloat(string s, float fallback)
    {
        Match m = Regex.Match(s, @"-?\d+(\.\d+)?");
        return m.Success && float.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : fallback;
    }

    // ─────────────── Generare intrebari ───────────────

    List<Question> BuildQuestions(List<StarInfo> stars, int count)
    {
        var candidates = new List<Question>();

        // --- Alegere multipla usoara: culoare / constelatie ---
        var mcFields = new (string key, string q)[]
        {
            ("Culoare",     "Ce culoare are {0}?"),
            ("Constelație", "În ce constelație se află {0}?"),
        };
        foreach (var star in stars)
        {
            foreach (var f in mcFields)
            {
                if (!star.fields.TryGetValue(f.key, out string val) || string.IsNullOrEmpty(val)) continue;
                var pool = new List<string>();
                foreach (var other in stars)
                    if (other != star && other.fields.TryGetValue(f.key, out string ov) && ov != val && !pool.Contains(ov))
                        pool.Add(ov);
                if (pool.Count < 3) continue;

                var opts = new List<string> { val };
                for (int k = 0; k < 3 && pool.Count > 0; k++)
                {
                    int idx = Random.Range(0, pool.Count);
                    opts.Add(pool[idx]); pool.RemoveAt(idx);
                }
                Shuffle(opts);
                candidates.Add(new Question
                {
                    text = string.Format(f.q, star.data.starName),
                    options = opts.ToArray(),
                    correct = opts.IndexOf(val),
                    explanation = $"{star.data.starName}: {f.key} = {val}."
                });
            }
        }

        // --- "Care e cea mai stralucitoare stea?" (alegere multipla) ---
        if (stars.Count >= 4)
        {
            StarInfo brightest = stars[0];
            foreach (var s in stars) if (s.mag < brightest.mag) brightest = s;

            var others = new List<StarInfo>(stars); others.Remove(brightest);
            Shuffle(others);
            var opts = new List<string> { brightest.data.starName };
            for (int k = 0; k < 3 && k < others.Count; k++) opts.Add(others[k].data.starName);
            if (opts.Count == 4)
            {
                Shuffle(opts);
                candidates.Add(new Question
                {
                    text = "Care dintre acestea e cea mai strălucitoare stea?",
                    options = opts.ToArray(),
                    correct = opts.IndexOf(brightest.data.starName),
                    explanation = $"{brightest.data.starName} e cea mai strălucitoare (magnitudine {brightest.mag})."
                });
            }
        }

        // --- "In ce emisfera suntem?" (din latitudine) ---
        if (planetariumManager != null)
        {
            float lat = planetariumManager.latitude;
            var opts = new List<string> { "Emisfera nordică", "Emisfera sudică" };
            int correct = lat >= 0f ? 0 : 1;
            candidates.Add(new Question
            {
                text = "În ce emisferă ne aflăm?",
                options = opts.ToArray(),
                correct = correct,
                explanation = $"Latitudinea observatorului e {lat:F1}°, deci suntem în {opts[correct].ToLower()}."
            });
        }

        // --- "Arata o stea din constelatia X" (point-at-star) ---
        var byCon = new Dictionary<string, int>();
        foreach (var s in stars)
            if (s.fields.TryGetValue("Constelație", out string con) && !string.IsNullOrEmpty(con))
                byCon[con] = byCon.TryGetValue(con, out int n) ? n + 1 : 1;
        foreach (var kv in byCon)
        {
            string con = kv.Key; // capturam local
            candidates.Add(new Question
            {
                isPoint = true,
                text = $"Arată cu laserul o stea din constelația {con}, apoi fă pinch.",
                explanation = $"Trebuia o stea din constelația {con}.",
                isCorrectStar = sd =>
                {
                    var fld = ParseDesc(sd.desc);
                    return fld.TryGetValue("Constelație", out string c) && c == con;
                }
            });
        }

        // --- "Arata cea mai stralucitoare stea" (point-at-star) ---
        if (stars.Count > 0)
        {
            StarInfo brightest = stars[0];
            foreach (var s in stars) if (s.mag < brightest.mag) brightest = s;
            StarData target = brightest.data;
            candidates.Add(new Question
            {
                isPoint = true,
                text = "Arată cu laserul cea mai strălucitoare stea, apoi fă pinch.",
                explanation = $"Cea mai strălucitoare era {target.starName}.",
                isCorrectStar = sd => sd == target
            });
        }

        Shuffle(candidates);
        return candidates.GetRange(0, Mathf.Min(count, candidates.Count));
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ─────────────── Intrebare cu alegere multipla ───────────────

    IEnumerator AskChoiceQuestion(Question q, int idx, int total, System.Action<bool> onResult)
    {
        GetLayout(out Vector3 basePos, out Quaternion rot);

        GameObject questionPanel = MakePanel("QuizQuestion", basePos + Vector3.up * questionHeight, rot,
            questionSize, panelColor, questionFontSize, out TextMeshPro qText, false);
        qText.text = $"<size=70%><color=#7FDBFF>Întrebarea {idx}/{total}</color></size>\n{q.text}";

        var buttons = new List<QuizOption>();
        for (int o = 0; o < q.options.Length; o++)
        {
            Vector3 pos = basePos + Vector3.up * (firstOptionOffset - o * optionSpacing);
            GameObject b = MakePanel($"QuizOption_{o}", pos, rot, optionSize, optionColor,
                optionFontSize, out TextMeshPro oText, true);
            oText.text = q.options[o];
            var opt = b.AddComponent<QuizOption>();
            opt.index = o;
            opt.background = b.GetComponentInChildren<MeshRenderer>();
            buttons.Add(opt);
        }

        Narrate(q.text);

        int chosen = -1;
        bool wasPinch = false;
        while (chosen < 0)
        {
            QuizOption hovered = RaycastComponent<QuizOption>();
            foreach (var b in buttons) SetColor(b.background, b == hovered ? hoverColor : optionColor);

            if (PinchEdge(ref wasPinch) && hovered != null) chosen = hovered.index;
            yield return null;
        }

        bool correct = chosen == q.correct;
        foreach (var b in buttons)
        {
            if (b.index == q.correct) SetColor(b.background, correctColor);
            else if (b.index == chosen) SetColor(b.background, wrongColor);
            else SetColor(b.background, optionColor);
        }
        qText.text = (correct ? "<color=#7CFC9A>Corect!</color>\n" : "<color=#FF8080>Greșit.</color>\n")
                   + $"<size=75%>{q.explanation}</size>";
        Narrate(correct ? "Corect!" : "Greșit. " + q.explanation);

        yield return new WaitForSeconds(feedbackSeconds);
        Destroy(questionPanel);
        foreach (var b in buttons) if (b != null) Destroy(b.gameObject);
        onResult?.Invoke(correct);
    }

    // ─────────────── Intrebare "arata o stea" ───────────────

    IEnumerator AskPointQuestion(Question q, int idx, int total, System.Action<bool> onResult)
    {
        GetLayout(out Vector3 basePos, out Quaternion rot);
        // panoul mai sus, ca sa lase cerul liber pentru cautat
        GameObject panel = MakePanel("QuizPoint", basePos + Vector3.up * (questionHeight + 0.25f), rot,
            questionSize, panelColor, questionFontSize, out TextMeshPro qText, false);
        qText.text = $"<size=70%><color=#7FDBFF>Întrebarea {idx}/{total}</color></size>\n{q.text}";
        Narrate(q.text);

        StarData picked = null;
        bool wasPinch = false;
        while (picked == null)
        {
            StarData hovered = RaycastComponent<StarData>();
            if (PinchEdge(ref wasPinch) && hovered != null) picked = hovered;
            yield return null;
        }

        bool correct = q.isCorrectStar != null && q.isCorrectStar(picked);
        qText.text = (correct ? "<color=#7CFC9A>Corect!</color>\n" : "<color=#FF8080>Greșit.</color>\n")
                   + $"<size=70%>Ai ales: {picked.starName}\n{q.explanation}</size>";
        Narrate(correct ? "Corect!" : "Greșit. " + q.explanation);

        yield return new WaitForSeconds(feedbackSeconds);
        Destroy(panel);
        onResult?.Invoke(correct);
    }

    IEnumerator ShowResult(int score, int total)
    {
        GetLayout(out Vector3 basePos, out Quaternion rot);
        GameObject panel = MakePanel("QuizResult", basePos + Vector3.up * 0.2f, rot,
            questionSize, panelColor, questionFontSize, out TextMeshPro t, false);
        t.text = $"<b>Scor final</b>\n<size=140%>{score} / {total}</size>";
        Narrate($"Ai răspuns corect la {score} din {total} întrebări.");
        yield return new WaitForSeconds(feedbackSeconds + 1f);
        Destroy(panel);
    }

    // ─────────────── Helperi ───────────────

    bool PinchEdge(ref bool wasPinch)
    {
        bool pinch = rightHand != null && rightHand.IsTracked && rightHand.GetFingerIsPinching(pinchFinger);
        bool edge = pinch && !wasPinch && Time.time >= lastPinchTime + pinchCooldown;
        wasPinch = pinch;
        if (edge) lastPinchTime = Time.time;
        return edge;
    }

    T RaycastComponent<T>() where T : Component
    {
        if (rightHand == null || !rightHand.IsTracked) return null;
        Transform p = rightHand.PointerPose;
        if (p == null) return null;
        if (Physics.Raycast(p.position, p.forward, out RaycastHit hit, raycastDistance))
            return hit.collider.GetComponentInParent<T>();
        return null;
    }

    void SetColor(MeshRenderer mr, Color c)
    {
        if (mr != null) mr.material.color = c;
    }

    void Narrate(string text)
    {
        if (speech != null && speech.IsAvailable && !string.IsNullOrEmpty(text))
            speech.Speak(text);
    }

    void GetLayout(out Vector3 basePos, out Quaternion rot)
    {
        Transform cam = GetCameraTransform();
        Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        basePos = (cam != null ? cam.position : Vector3.zero) + fwd * distance;
        rot = Quaternion.LookRotation(fwd, Vector3.up);
    }

    GameObject MakePanel(string name, Vector3 worldPos, Quaternion rot, Vector2 size, Color bg,
                         float fontSize, out TextMeshPro tmp, bool withCollider)
    {
        GameObject root = new GameObject(name);
        root.transform.position = worldPos;
        root.transform.rotation = rot;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "bg";
        quad.transform.SetParent(root.transform, false);
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        var mr = quad.GetComponent<MeshRenderer>();
        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default") ?? Shader.Find("Unlit/Color");
        mr.material = new Material(sh);
        mr.material.color = bg;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        if (!withCollider)
        {
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        GameObject tObj = new GameObject("txt");
        tObj.transform.SetParent(root.transform, false);
        tObj.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        tmp = tObj.AddComponent<TextMeshPro>();
        tmp.rectTransform.sizeDelta = new Vector2(size.x * 0.92f, size.y * 0.9f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.02f;
        tmp.fontSizeMax = fontSize;
        tmp.color = Color.white;
        return root;
    }

    Transform GetCameraTransform()
    {
        GameObject centerEye = GameObject.Find("CenterEyeAnchor");
        if (centerEye != null) return centerEye.transform;
        return Camera.main != null ? Camera.main.transform : null;
    }
}
