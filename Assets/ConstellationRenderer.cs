using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class ConstellationRenderer : MonoBehaviour
{
    public PlanetariumManager planetariumManager;

    [Header("Line Appearance")]
    public Color lineColor = new Color(0.4f, 0.8f, 1f, 0.5f);
    public float lineWidth = 0.5f;
    public Material lineMaterial;

    [Header("Debug")]
    public bool showMatchedStarNames = false;

    private List<ConstellationLineInstance> lines = new List<ConstellationLineInstance>();
    private Dictionary<string, Transform> starLookup = new Dictionary<string, Transform>();

    private struct ConstellationLineInstance
    {
        public LineRenderer renderer;
        public Transform star1;
        public Transform star2;
    }

    private static readonly Dictionary<string, string> properNameToBayer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        {"Sirius", "alf cma"},
        {"Canopus", "alf car"},
        {"Rigil Kentaurus", "alf cen"},
        {"Toliman", "alf cen"},
        {"Arcturus", "alf boo"},
        {"Vega", "alf lyr"},
        {"Capella", "alf aur"},
        {"Rigel", "bet ori"},
        {"Betelgeuse", "alf ori"},
        {"Altair", "alf aql"},
        {"Aldebaran", "alf tau"},
        {"Pollux", "bet gem"},
        {"Spica", "alf vir"},
        {"Antares", "alf sco"},
        {"Fomalhaut", "alf psa"},
        {"Deneb", "alf cyg"},
        {"Regulus", "alf leo"},
        {"Procyon", "alf cmi"},
        {"Procyon A", "alf cmi"},
        {"Achernar", "alf eri"},
        {"Hadar", "bet cen"},
        {"Mimosa", "bet cru"},
        {"Shaula", "lam sco"},
        {"Bellatrix", "gam ori"},
        {"Elnath", "bet tau"},
        {"Alnilam", "eps ori"},
        {"Miaplacidus", "bet car"},
        {"Alnair", "alf gru"},
        {"Alioth", "eps uma"},
        {"Alnitak", "zet ori"},
        {"Mirfak", "alf per"},
        {"Dubhe", "alf uma"},
        {"Wezen", "del cma"},
        {"Sargas", "tet sco"},
        {"Alcaid", "eta uma"},
        {"Avior", "eps car"},
        {"Atria", "alf tra"},
        {"Menkalinan", "bet aur"},
        {"Castor", "alf gem"},
        {"Castor A", "alf gem"},
        {"Castor B", "alf gem"},
        {"Castor AB", "alf gem"},
        {"Peacock", "alf pav"},
        {"Alhena", "gam gem"},
        {"Mirzam", "bet cma"},
        {"Alphard", "alf hya"},
        {"Hamal", "alf ari"},
        {"Diphda", "bet cet"},
        {"Polaris", "alf umi"},
        {"North Star", "alf umi"},
        {"Menkent", "tet cen"},
        {"Saiph", "kap ori"},
        {"Sirrah", "alf and"},
        {"Nunki", "sig sgr"},
        {"Rasalhague", "alf oph"},
        {"Kochab", "bet umi"},
        {"Almach", "gam and"},
        {"Mirach", "bet and"},
        {"Schedar", "alf cas"},
        {"Caph", "bet cas"},
        {"Denebola", "bet leo"},
        {"Zosma", "del leo"},
        {"Algieba", "gam leo"},
        {"Muphrid", "eta boo"},
        {"Izar", "eps boo"},
        {"Skat", "del aqr"},
        {"Sadalmelik", "alf aqr"},
        {"Enif", "eps peg"},
        {"Markab", "alf peg"},
        {"Scheat", "bet peg"},
        {"Algenib", "gam peg"},
        {"Alderamin", "alf cep"},
        {"Eltanin", "gam dra"},
        {"Rastaban", "bet dra"},
        {"Grumium", "ksi dra"},
        {"Thuban", "alf dra"},
        {"Kraz", "bet crv"},
        {"Algorab", "del crv"},
        {"Minkar", "eps crv"},
        {"Vindemiatrix", "eps vir"},
        {"Porrima", "gam vir"},
        {"Zubenelgenubi", "alf lib"},
        {"Zubeneschamali", "bet lib"},
        {"Unukalhai", "alf ser"},
        {"Cebalrai", "bet oph"},
        {"Yed Prior", "del oph"},
        {"Yed Posterior", "eps oph"},
        {"Tarazed", "gam aql"},
        {"Gienah", "eps cyg"},
        {"Sadr", "gam cyg"},
        {"Al Fawaris", "del cyg"},
        {"Ruchbah", "del cas"},
        {"Segin", "eps cas"},
        {"Navi", "gam cas"},
        {"Merak", "bet uma"},
        {"Phecda", "gam uma"},
        {"Megrez", "del uma"},
        {"Mizar", "zet uma"},
        {"Mizar A", "zet uma"},
        {"Alkaid", "eta uma"},
        {"Gomeisa", "bet cmi"},
        {"Adhara", "eps cma"},
        {"Aludra", "eta cma"},
        {"Furud", "zet cma"},
        {"Tejat", "eta gem"},
        {"Mebsuta", "eps gem"},
        {"Alcyone", "eta tau"},
        {"Asterope", "21 tau"},
        {"Maia", "20 tau"},
        {"Merope", "23 tau"},
        {"Taygeta", "19 tau"},
        {"Electra", "17 tau"},
        {"Celaeno", "16 tau"},
        {"Taurus Poniatovii", "lam sgr"},
        {"Kaus Borealis", "lam sgr"},
        {"Kaus Media", "del sgr"},
        {"Kaus Australis", "eps sgr"},
        {"Ascella", "zet sgr"},
        {"Nash", "gam sgr"},
        {"Alnasl", "gam sgr"},
        {"Dschubba", "del sco"},
        {"Lesath", "ups sco"},
        {"Paikauhale", "tau sco"},
        {"Sabik", "eta oph"},
        {"Marfik", "lam oph"},
        {"Alphecca", "alf crb"},
        {"Nihal", "bet lep"},
        {"Arneb", "alf lep"},
        {"Phact", "alf col"},
        {"Markeb", "kap vel"},
        {"Suhail", "lam vel"},
        {"Aspidiske", "iot car"},
        {"Tureis", "rho pup"},
        {"Naos", "zet pup"},
        {"Ankaa", "alf phe"},
        {"Acamar", "tet eri"},
        {"Cursa", "bet eri"},
        {"Zaurak", "gam eri"},
        {"Sheratan", "bet ari"},
        {"Mesarthim", "gam ari"},
        {"Cor Caroli", "alf cvn"},
        {"Hassaleh", "iot aur"},
        {"Mahasim", "tet aur"},
        {"Almaaz", "eps aur"},
        {"Mintaka", "del ori"},
        {"Albireo", "bet cyg"},
        {"Albaldah", "pi sgr"},
        {"Deneb Algedi", "del cap"},
        {"Kornephoros", "bet her"},
        {"Hatysa", "iot ori"},
        {"Imai", "del cru"},
        {"Menkar", "alf cet"},
        {"Gacrux", "gam cru"},
        {"Regor", "gam vel"},
        {"Dabih", "bet cap"},
        {"Algedi", "alf cap"},
        {"Nashira", "gam cap"},
        {"Sadalsuud", "bet aqr"},
        {"Sadalachbia", "gam aqr"},
        {"Homam", "zet peg"},
        {"Baham", "tet peg"},
        {"Al Kalb al Rai", "ksi cep"},
        {"Errai", "gam cep"},
        {"Kitalpha", "alf equ"},
        {"Sulafat", "gam lyr"},
        {"Sheliak", "bet lyr"},
        {"Rukbat", "alf sgr"},
        {"Arkab", "bet sgr"},
        {"Alsafi", "sig dra"},
        {"Dziban", "psi dra"},
        {"Tegmine", "zet cnc"},
        {"Acubens", "alf cnc"},
        {"Asellus Australis", "del cnc"},
        {"Asellus Borealis", "gam cnc"},
        {"Al Tarf", "bet cnc"},
        {"Wasat", "del gem"},
        {"Mekbuda", "zet gem"},
        {"Rotanev", "bet del"},
        {"Sualocin", "alf del"},
        {"Aldulfin", "eps del"},
        {"Altais", "del dra"},
        {"Tyl", "eps dra"},
        {"Gianfar", "lam dra"},
        {"Edasich", "iot dra"},
        {"Zaniah", "eta vir"},
        {"Syrma", "iot vir"},
        {"Rijl al Awwa", "mu vir"},
        {"Zubenelakribi", "del lib"},
        {"Brachium", "sig lib"},
        {"Rasalas", "mu leo"},
        {"Adhafera", "zet leo"},
        {"Chort", "tet leo"},
        {"Misam", "kap per"},
        {"Atik", "omi per"},
        {"Menkib", "ksi per"},
        {"Seginus", "gam boo"},
        {"Nekkar", "bet boo"},
        {"Alkalurops", "mu boo"},
        {"Xuange", "lam boo"},
        {"Zubenelakrab", "gam lib"},
        {"Pherkad", "gam umi"},
        {"Yildun", "del umi"},
        {"Fang", "pi sco"},
        {"Iclil", "rho sco"},
        {"Jabbah", "nu sco"},
        {"Alniyat", "sig sco"},
    };

    void Start()
    {
        if (planetariumManager == null)
            planetariumManager = FindObjectOfType<PlanetariumManager>();

        if (planetariumManager == null)
        {
            Debug.LogError("ConstellationRenderer: PlanetariumManager nu a fost găsit!");
            return;
        }

        if (planetariumManager.Stars.Count > 0)
        {
            BuildStarLookup();
            CreateConstellationLines();
        }
        else
        {
            planetariumManager.OnStarsLoaded += OnStarsLoaded;
        }
    }

    void OnStarsLoaded()
    {
        planetariumManager.OnStarsLoaded -= OnStarsLoaded;
        BuildStarLookup();
        CreateConstellationLines();
    }

    void BuildStarLookup()
    {
        starLookup.Clear();

        foreach (var star in planetariumManager.Stars)
        {
            string key = ExtractBayerDesignation(star.name);
            if (key == null)
                key = ProperNameToBayer(star.name);

            if (key != null)
            {
                if (!starLookup.ContainsKey(key))
                    starLookup[key] = star.starObject.transform;

                if (showMatchedStarNames)
                    Debug.Log($"Constellation match: \"{star.name}\" → \"{key}\"");
            }
        }
    }

    string ExtractBayerDesignation(string starName)
    {
        if (string.IsNullOrEmpty(starName)) return null;

        string clean = starName;
        int vmagIndex = clean.IndexOf("(Vmag:", StringComparison.Ordinal);
        if (vmagIndex >= 0)
            clean = clean.Substring(0, vmagIndex).Trim();

        clean = clean.TrimEnd(')').Trim();

        if (!clean.StartsWith("* "))
        {
            clean = clean.Replace("-IAU ", "").Trim();
            return null;
        }

        clean = clean.Substring(2).Trim();

        string[] parts = clean.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        string bayer = parts[0].ToLowerInvariant();
        string constellation = parts[1];

        bayer = bayer.Replace(".", "");

        if (bayer.Length > 1 && char.IsDigit(bayer[bayer.Length - 1]))
            bayer = bayer.Substring(0, bayer.Length - 1);

        return bayer + " " + constellation.ToLowerInvariant();
    }

    string ProperNameToBayer(string starName)
    {
        if (string.IsNullOrEmpty(starName)) return null;

        string clean = starName;
        int vmagIndex = clean.IndexOf("(Vmag:", StringComparison.Ordinal);
        if (vmagIndex >= 0)
            clean = clean.Substring(0, vmagIndex).Trim();

        clean = clean.Replace("-IAU ", "").Trim();

        string[] parts = clean.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        string properName = parts[0].Trim();

        if (properNameToBayer.TryGetValue(properName, out string bayerKey))
            return bayerKey;

        if (parts.Length >= 2)
        {
            string twoPartName = parts[0].Trim() + " " + parts[1].Trim();
            if (properNameToBayer.TryGetValue(twoPartName, out bayerKey))
                return bayerKey;
        }

        return null;
    }

    void CreateConstellationLines()
    {
        foreach (var lineSet in constellationData)
        {
            string key1 = lineSet.star1.ToLowerInvariant();
            string key2 = lineSet.star2.ToLowerInvariant();

            if (!starLookup.TryGetValue(key1, out Transform t1)) continue;
            if (!starLookup.TryGetValue(key2, out Transform t2)) continue;

            GameObject lineObj = new GameObject($"ConstLine_{key1}-{key2}");
            lineObj.transform.SetParent(transform, false);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, t1.position);
            lr.SetPosition(1, t2.position);

            if (lineMaterial != null)
                lr.material = lineMaterial;
            else
                lr.material = new Material(Shader.Find("Unlit/Color")) { color = lineColor };

            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            lines.Add(new ConstellationLineInstance { renderer = lr, star1 = t1, star2 = t2 });
        }

        Debug.Log($"ConstellationRenderer: {starLookup.Count} stele potrivite, {lines.Count} linii desenate din {constellationData.Length} segmente definite");

        if (showMatchedStarNames)
        {
            foreach (var kvp in starLookup)
                Debug.Log($"  → {kvp.Key}");
        }
    }

    public void SetConstellationsVisible(bool visible)
    {
        foreach (var line in lines)
        {
            if (line.renderer != null)
                line.renderer.enabled = visible;
        }
    }

    void Update()
    {
        foreach (var line in lines)
        {
            if (line.renderer == null) continue;
            line.renderer.SetPosition(0, line.star1.position);
            line.renderer.SetPosition(1, line.star2.position);
        }
    }

    // ─── DATE CONSTELAȚII: segmente definite prin Bayer + abrevierea constelației ───

    private struct ConstLine { public string star1; public string star2; public ConstLine(string s1, string s2) { star1 = s1; star2 = s2; } }

    private static readonly ConstLine[] constellationData = new ConstLine[]
    {
        // === Ursa Major (UMa) - Carul Mare ===
        new("alf uma", "bet uma"),
        new("bet uma", "gam uma"),
        new("gam uma", "del uma"),
        new("del uma", "eps uma"),
        new("eps uma", "zet uma"),
        new("zet uma", "eta uma"),

        // === Ursa Minor (UMi) - Carul Mic ===
        new("alf umi", "del umi"),
        new("del umi", "eps umi"),
        new("eps umi", "zet umi"),
        new("zet umi", "eta umi"),
        new("eta umi", "gam umi"),
        new("gam umi", "bet umi"),

        // === Cassiopeia (Cas) ===
        new("bet cas", "alf cas"),
        new("alf cas", "gam cas"),
        new("gam cas", "del cas"),
        new("del cas", "eps cas"),

        // === Cepheus (Cep) ===
        new("alf cep", "bet cep"),
        new("bet cep", "gam cep"),
        new("gam cep", "del cep"),
        new("del cep", "eps cep"),
        new("eps cep", "zet cep"),
        new("zet cep", "alf cep"),

        // === Draco (Dra) ===
        new("alf dra", "bet dra"),
        new("bet dra", "gam dra"),
        new("gam dra", "del dra"),
        new("del dra", "eps dra"),
        new("eps dra", "zet dra"),
        new("zet dra", "eta dra"),

        // === Orion (Ori) ===
        new("bet ori", "alf ori"),
        new("bet ori", "gam ori"),
        new("gam ori", "del ori"),
        new("del ori", "eps ori"),
        new("eps ori", "zet ori"),
        new("zet ori", "eta ori"),
        new("eta ori", "alf ori"),

        // === Canis Major (CMa) ===
        new("alf cma", "bet cma"),
        new("bet cma", "gam cma"),
        new("gam cma", "del cma"),
        new("del cma", "eps cma"),

        // === Canis Minor (CMi) ===
        new("alf cmi", "bet cmi"),

        // === Gemini (Gem) ===
        new("alf gem", "bet gem"),
        new("alf gem", "gam gem"),
        new("bet gem", "del gem"),
        new("del gem", "eps gem"),
        new("eps gem", "zet gem"),

        // === Taurus (Tau) ===
        new("alf tau", "bet tau"),
        new("bet tau", "gam tau"),
        new("gam tau", "del tau"),
        new("del tau", "eps tau"),
        new("eps tau", "zet tau"),

        // === Auriga (Aur) ===
        new("alf aur", "bet aur"),
        new("bet aur", "gam aur"),
        new("gam aur", "del aur"),
        new("del aur", "eps aur"),

        // === Leo (Leo) ===
        new("alf leo", "eta leo"),
        new("eta leo", "gam leo"),
        new("gam leo", "zet leo"),
        new("zet leo", "mu leo"),
        new("mu leo", "eps leo"),
        new("eps leo", "alf leo"),
        new("alf leo", "bet leo"),
        new("bet leo", "del leo"),

        // === Virgo (Vir) ===
        new("alf vir", "gam vir"),
        new("gam vir", "eps vir"),
        new("eps vir", "del vir"),
        new("del vir", "zet vir"),
        new("zet vir", "bet vir"),
        new("bet vir", "eta vir"),
        new("eta vir", "gam vir"),

        // === Boötes (Boo) ===
        new("alf boo", "eps boo"),
        new("eps boo", "zet boo"),
        new("zet boo", "eta boo"),
        new("eta boo", "bet boo"),
        new("bet boo", "gam boo"),
        new("gam boo", "del boo"),
        new("del boo", "mu boo"),

        // === Corona Borealis (CrB) ===
        new("alf crb", "bet crb"),
        new("bet crb", "gam crb"),
        new("gam crb", "del crb"),
        new("del crb", "eps crb"),
        new("eps crb", "zet crb"),
        new("zet crb", "eta crb"),

        // === Hercules (Her) ===
        new("alf her", "bet her"),
        new("bet her", "gam her"),
        new("gam her", "del her"),
        new("del her", "eps her"),
        new("eps her", "zet her"),
        new("zet her", "eta her"),
        new("eta her", "alf her"),
        new("alf her", "eps her"),

        // === Lyra (Lyr) ===
        new("alf lyr", "eps lyr"),
        new("eps lyr", "zet lyr"),
        new("zet lyr", "del lyr"),
        new("del lyr", "bet lyr"),
        new("bet lyr", "gam lyr"),

        // === Cygnus (Cyg) ===
        new("alf cyg", "bet cyg"),
        new("bet cyg", "gam cyg"),
        new("gam cyg", "del cyg"),
        new("del cyg", "eps cyg"),
        new("eps cyg", "zet cyg"),

        // === Aquila (Aql) ===
        new("alf aql", "bet aql"),
        new("bet aql", "gam aql"),
        new("gam aql", "del aql"),
        new("del aql", "eps aql"),
        new("eps aql", "zet aql"),

        // === Delphinus (Del) ===
        new("alf del", "bet del"),
        new("bet del", "gam del"),
        new("gam del", "del del"),
        new("del del", "eps del"),
        new("eps del", "alf del"),

        // === Ophiuchus (Oph) ===
        new("alf oph", "del oph"),
        new("del oph", "eps oph"),
        new("eps oph", "eta oph"),
        new("eta oph", "zet oph"),
        new("zet oph", "bet oph"),
        new("bet oph", "gam oph"),
        new("gam oph", "alf oph"),

        // === Scorpius (Sco) ===
        new("alf sco", "bet sco"),
        new("bet sco", "del sco"),
        new("del sco", "eps sco"),
        new("eps sco", "eta sco"),
        new("eta sco", "zet sco"),
        new("zet sco", "mu sco"),
        new("mu sco", "gam sco"),
        new("gam sco", "kap sco"),
        new("kap sco", "iot sco"),

        // === Sagittarius (Sgr) ===
        new("alf sgr", "bet sgr"),
        new("bet sgr", "gam sgr"),
        new("gam sgr", "del sgr"),
        new("del sgr", "eps sgr"),
        new("eps sgr", "zet sgr"),
        new("zet sgr", "eta sgr"),

        // === Pegasus (Peg) ===
        new("alf peg", "bet peg"),
        new("bet peg", "gam peg"),
        new("gam peg", "alf peg"),

        // === Andromeda (And) ===
        new("alf and", "del and"),
        new("del and", "bet and"),
        new("bet and", "gam and"),

        // === Perseus (Per) ===
        new("alf per", "bet per"),
        new("bet per", "gam per"),
        new("gam per", "del per"),
        new("del per", "eps per"),
        new("eps per", "zet per"),

        // === Triangulum (Tri) ===
        new("alf tri", "bet tri"),
        new("bet tri", "gam tri"),
        new("gam tri", "alf tri"),

        // === Aries (Ari) ===
        new("alf ari", "bet ari"),
        new("bet ari", "gam ari"),

        // === Cetus (Cet) ===
        new("alf cet", "gam cet"),
        new("gam cet", "del cet"),
        new("del cet", "mu cet"),
        new("mu cet", "zet cet"),
        new("zet cet", "tau cet"),
        new("tau cet", "alf cet"),

        // === Hydra (Hya) ===
        new("alf hya", "eps hya"),
        new("eps hya", "zet hya"),
        new("zet hya", "eta hya"),
        new("eta hya", "gam hya"),
        new("gam hya", "del hya"),

        // === Corvus (Crv) ===
        new("alf crv", "bet crv"),
        new("bet crv", "gam crv"),
        new("gam crv", "del crv"),
        new("del crv", "alf crv"),

        // === Crater (Crt) ===
        new("alf crt", "bet crt"),
        new("bet crt", "gam crt"),
        new("gam crt", "del crt"),
        new("del crt", "alf crt"),

        // === Libra (Lib) ===
        new("alf lib", "bet lib"),
        new("bet lib", "gam lib"),
        new("gam lib", "del lib"),
        new("del lib", "alf lib"),

        // === Capricornus (Cap) ===
        new("alf cap", "bet cap"),
        new("bet cap", "gam cap"),
        new("gam cap", "del cap"),
        new("del cap", "eps cap"),
        new("eps cap", "zet cap"),

        // === Aquarius (Aqr) ===
        new("alf aqr", "bet aqr"),
        new("bet aqr", "gam aqr"),
        new("gam aqr", "del aqr"),
        new("del aqr", "eps aqr"),
        new("eps aqr", "zet aqr"),

        // === Pisces (Psc) ===
        new("alf psc", "bet psc"),
        new("bet psc", "gam psc"),
        new("gam psc", "del psc"),
        new("del psc", "eps psc"),
        new("eps psc", "zet psc"),

        // === Eridanus (Eri) ===
        new("alf eri", "bet eri"),
        new("bet eri", "gam eri"),
        new("gam eri", "del eri"),
        new("del eri", "eps eri"),
        new("eps eri", "zet eri"),

        // === Serpens (Ser) ===
        new("alf ser", "bet ser"),
        new("bet ser", "gam ser"),
        new("gam ser", "del ser"),
        new("del ser", "eps ser"),

        // === Coma Berenices (Com) ===
        new("alf com", "bet com"),
        new("bet com", "gam com"),

        // === Canes Venatici (CVn) ===
        new("alf cvn", "bet cvn"),

        // === Sagitta (Sge) ===
        new("alf sge", "bet sge"),
        new("bet sge", "gam sge"),
        new("gam sge", "del sge"),

        // === Vulpecula (Vul) ===
        new("alf vul", "bet vul"),

        // === Scutum (Sct) ===
        new("alf sct", "bet sct"),
        new("bet sct", "gam sct"),
        new("gam sct", "del sct"),

        // === Lynx (Lyn) ===
        new("alf lyn", "bet lyn"),

        // === Leo Minor (LMi) ===
        new("bet lmi", "lm lmi"),

        // === Camelopardalis (Cam) ===
        new("alf cam", "bet cam"),
        new("bet cam", "gam cam"),

        // === Monoceros (Mon) ===
        new("alf mon", "bet mon"),
        new("bet mon", "gam mon"),

        // === Lepus (Lep) ===
        new("alf lep", "bet lep"),
        new("bet lep", "gam lep"),
        new("gam lep", "del lep"),
        new("del lep", "eps lep"),

        // === Centaurus (Cen) ===
        new("alf cen", "bet cen"),
        new("bet cen", "gam cen"),
        new("gam cen", "del cen"),
        new("del cen", "eps cen"),
        new("eps cen", "zet cen"),

        // === Crux (Cru) ===
        new("alf cru", "bet cru"),
        new("bet cru", "gam cru"),
        new("gam cru", "del cru"),
        new("del cru", "alf cru"),

        // === Carina (Car) ===
        new("alf car", "bet car"),
        new("bet car", "eps car"),
        new("eps car", "iot car"),
        new("iot car", "tet car"),

        // === Vela (Vel) ===
        new("alf vel", "bet vel"),
        new("bet vel", "gam vel"),
        new("gam vel", "del vel"),
        new("del vel", "mu vel"),

        // === Puppis (Pup) ===
        new("alf pup", "bet pup"),
        new("bet pup", "gam pup"),
        new("gam pup", "del pup"),

        // === Corona Australis (CrA) ===
        new("alf cra", "bet cra"),
        new("bet cra", "gam cra"),
        new("gam cra", "del cra"),
        new("del cra", "eps cra"),
        new("eps cra", "zet cra"),

        // === Indus (Ind) ===
        new("alf ind", "bet ind"),

        // === Pavo (Pav) ===
        new("alf pav", "bet pav"),
        new("bet pav", "gam pav"),
        new("gam pav", "del pav"),

        // === Grus (Gru) ===
        new("alf gru", "bet gru"),
        new("bet gru", "gam gru"),
        new("gam gru", "del gru"),

        // === Tucana (Tuc) ===
        new("alf tuc", "bet tuc"),
        new("bet tuc", "gam tuc"),

        // === Phoenix (Phe) ===
        new("alf phe", "bet phe"),
        new("bet phe", "gam phe"),
        new("gam phe", "del phe"),

        // === Fornax (For) ===
        new("alf for", "bet for"),

        // === Sculptor (Scl) ===
        new("alf scl", "bet scl"),
        new("bet scl", "gam scl"),

        // === Pyxis (Pyx) ===
        new("alf pyx", "bet pyx"),
        new("bet pyx", "gam pyx"),

        // === Antlia (Ant) ===
        new("alf ant", "eps ant"),

        // === Sextans (Sex) ===
        new("alf sex", "bet sex"),

        // === Norma (Nor) ===
        new("alf nor", "bet nor"),
        new("bet nor", "gam nor"),

        // === Circinus (Cir) ===
        new("alf cir", "bet cir"),

        // === Triangulum Australe (TrA) ===
        new("alf tra", "bet tra"),
        new("bet tra", "gam tra"),
        new("gam tra", "alf tra"),

        // === Ara (Ara) ===
        new("alf ara", "bet ara"),
        new("bet ara", "gam ara"),
        new("gam ara", "del ara"),

        // === Telescopium (Tel) ===
        new("alf tel", "bet tel"),

        // === Microscopium (Mic) ===
        new("alf mic", "bet mic"),

        // === Piscis Austrinus (PsA) ===
        new("alf psa", "bet psa"),
        new("bet psa", "gam psa"),

        // === Columba (Col) ===
        new("alf col", "bet col"),
        new("bet col", "gam col"),

        // === Dorado (Dor) ===
        new("alf dor", "bet dor"),

        // === Volans (Vol) ===
        new("alf vol", "bet vol"),
        new("bet vol", "gam vol"),
    };
}
