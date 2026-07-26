using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

/// <summary>
/// Quiz cu intrebari conceptuale despre cer (alegere multipla), potrivite pe lectii.
/// Intrebarile sunt un set fix; la fiecare quiz se aleg aleator cateva.
/// Raspuns prin laser + pinch (mana dreapta). Feedback (corect/gresit + raspuns) si scor.
/// </summary>
public class QuizManager : MonoBehaviour
{
    [Header("Referinte")]
    public OVRHand rightHand;           // aceeasi mana ca AstroLaser
    public AstroLaser astroLaser;       // il suprimam in timpul quiz-ului
    public TextToSpeechManager speech;  // optional, pentru narare

    [Header("Setari")]
    public int questionsPerQuiz = 5;
    public OVRHand.HandFinger pinchFinger = OVRHand.HandFinger.Index;
    public float pinchCooldown = 0.6f;
    public float feedbackSeconds = 2.5f;
    public float raycastDistance = 100f;

    [Header("Asezare (in fata camerei)")]
    public float distance = 2.5f;
    public float questionHeight = 0.46f;
    public float firstOptionOffset = 0.10f;
    public float optionSpacing = 0.26f;
    public Vector2 questionSize = new Vector2(1.3f, 0.36f);
    public Vector2 optionSize = new Vector2(1.25f, 0.22f);
    // Folosite ca font MAXIM; textul se auto-dimensioneaza sa umple panoul.
    public float questionFontSize = 0.09f;
    public float optionFontSize = 0.08f;

    [Header("Culori")]
    public Color panelColor = new Color(0f, 0f, 0f, 0.85f);
    public Color optionColor = new Color(0.10f, 0.15f, 0.30f, 0.92f);
    public Color hoverColor = new Color(0.20f, 0.35f, 0.60f, 0.95f);
    public Color correctColor = new Color(0.15f, 0.55f, 0.20f, 0.97f);
    public Color wrongColor = new Color(0.70f, 0.15f, 0.15f, 0.97f);

    private bool quizActive;
    private float lastPinchTime;

    private class Question
    {
        public string text;
        public string[] options;
        public int correct;
    }

    // Setul de intrebari. PRIMA optiune din fiecare rand e raspunsul CORECT
    // (se amesteca automat la afisare). Usor de extins.
    private static readonly (string q, string[] options)[] Pool = new (string, string[])[]
    {
        ("De ce se mișcă stelele pe cer în timpul nopții?",
            new[]{"Pentru că Pământul se rotește", "Pentru că stelele zboară", "Pentru că se mișcă Luna"}),
        ("În ce direcție răsar stelele și Soarele?",
            new[]{"De la est", "De la vest", "De la nord"}),
        ("Ce se întâmplă cu stelele privite de la Polul Nord?",
            new[]{"Se rotesc în cerc, fără să răsară sau să apună", "Răsar și apun ca la noi", "Stau nemișcate"}),
        ("Steaua Polară se află aproape deasupra capului la…?",
            new[]{"Polul Nord", "Ecuator", "Polul Sud"}),
        ("Cu cât mergem mai spre nord, Steaua Polară pe cer…?",
            new[]{"Urcă mai sus", "Coboară", "Dispare"}),
        ("De ce vedem alte constelații în emisfera sudică?",
            new[]{"Pentru că privim spre altă parte a bolții cerești", "Pentru că e mai cald", "Pentru că e ziua"}),
        ("La ecuator, de-a lungul unui an, câte constelații putem vedea?",
            new[]{"Aproape toate", "Doar jumătate", "Doar câteva"}),
        ("De ce vedem constelații diferite în fiecare anotimp?",
            new[]{"Pentru că Pământul se învârte în jurul Soarelui", "Pentru că stelele dispar", "Pentru că se schimbă vremea"}),
        ("Ce este o constelație?",
            new[]{"Un grup de stele care formează un desen pe cer", "O singură stea foarte mare", "O planetă"}),
        ("Ce înseamnă „latitudine nordică”?",
            new[]{"Că ne aflăm în emisfera nordică", "Că e frig", "Că suntem la Polul Nord"}),
        ("Soarele este de fapt…?",
            new[]{"O stea", "O planetă", "Un satelit"}),
        ("De ce e cerul întunecat noaptea?",
            new[]{"Pentru că Pământul e întors dinspre Soare", "Pentru că stelele se sting", "Pentru că Luna acoperă Soarele"}),
        ("Ce se întâmplă când accelerăm timpul într-un scenariu?",
            new[]{"Vedem cerul mișcându-se mult mai repede", "Ne apropiem de stele", "Cerul se luminează"}),
        ("La ecuator, stelele răsar și apun…?",
            new[]{"Aproape drept, în sus și în jos", "În cerc, fără să apună", "Rămân pe loc"}),
        ("Ce constelație celebră se vede din emisfera sudică (Sydney), dar nu de la noi?",
            new[]{"Crucea Sudului", "Ursa Mare", "Orion"}),
        ("Câte stele principale are Carul Mare (Ursa Mare)?",
            new[]{"7", "100", "2"}),
        ("Banda albicioasă de lumină de pe cerul întunecat se numește…?",
            new[]{"Calea Lactee", "Curcubeu", "Auroră"}),
        ("La Polul Sud, în locul Stelei Polare, cerul…?",
            new[]{"Se rotește în jurul polului sud, fără o stea polară strălucitoare", "Are tot Steaua Polară", "Are Soarele mereu sus"}),
    };

    // ─────────────── API public ───────────────

    public void StartQuiz()
    {
        if (!quizActive) StartCoroutine(RunQuiz());
    }

    public IEnumerator RunQuiz()
    {
        if (quizActive) yield break;
        quizActive = true;

        List<Question> questions = BuildQuestions(questionsPerQuiz);
        if (astroLaser != null) astroLaser.suppressPinchAction = true;

        int score = 0;
        for (int i = 0; i < questions.Count; i++)
        {
            bool ok = false;
            yield return StartCoroutine(AskQuestion(questions[i], i + 1, questions.Count, r => ok = r));
            if (ok) score++;
        }

        yield return StartCoroutine(ShowResult(score, questions.Count));

        if (astroLaser != null) astroLaser.suppressPinchAction = false;
        quizActive = false;
    }

    // ─────────────── Construire intrebari ───────────────

    List<Question> BuildQuestions(int count)
    {
        var all = new List<Question>();
        foreach (var (q, opts) in Pool)
        {
            string correctText = opts[0];             // prima e cea corecta
            var shuffled = new List<string>(opts);
            Shuffle(shuffled);
            all.Add(new Question
            {
                text = q,
                options = shuffled.ToArray(),
                correct = shuffled.IndexOf(correctText)
            });
        }
        Shuffle(all);
        return all.GetRange(0, Mathf.Min(count, all.Count));
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

    IEnumerator AskQuestion(Question q, int idx, int total, System.Action<bool> onResult)
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
                   + $"<size=75%>Răspuns corect: {q.options[q.correct]}</size>";
        Narrate(correct ? "Corect!" : "Greșit. Răspuns corect: " + q.options[q.correct]);

        yield return new WaitForSeconds(feedbackSeconds);
        Destroy(questionPanel);
        foreach (var b in buttons) if (b != null) Destroy(b.gameObject);
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
