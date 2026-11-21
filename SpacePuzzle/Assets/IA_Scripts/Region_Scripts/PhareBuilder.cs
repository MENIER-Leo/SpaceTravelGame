using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
#endif

// Construit en un clic (hors Play) le Phare et son puzzle sous cette ancre, puis le VÉRIFIE avec le vrai BeamManager.
// Bleu + Vert (via la plante) → Melangeur_Cyan → Foyer_Phare ; Rouge → Relais_Chaud (surchauffe) → Foyer_Phare.
// Cyan + Rouge = Blanc, à tenir quelques secondes : le Phare s'allume, fin du jeu.
// Les trois émetteurs apparaissent (cinématique) quand leur région s'allume.
// Coordonnées locales : X sur le côté, Z vers l'avant de l'ancre. Les distances suivent la surface.
public class PhareBuilder : MonoBehaviour
{
    [Header("Prefabs (composant Crystal à la racine)")]
    [SerializeField] private GameObject emitterPrefab;
    [SerializeField] private GameObject relayPrefab;
    [SerializeField] private GameObject receiverPrefab;
    [Tooltip("Vide = Relay Prefab réglé en mélangeur.")]
    [SerializeField] private GameObject mixerPrefab;
    [Tooltip("Vide = Relay Prefab. Mets le prefab de plante de la région Verte si tu en as un.")]
    [SerializeField] private GameObject plantPrefab;

    [Header("Mécaniques")]
    [SerializeField] private float heatTime = 4f;
    [SerializeField] private float coolRate = 1f;
    [Tooltip("Secondes de blanc pour allumer le Phare. Doit rester < Heat Time.")]
    [SerializeField] private float chargeTime = 3f;
    [Tooltip("Charge perdue par seconde sans blanc (élevé = il faut tenir d'une traite).")]
    [SerializeField] private float decayRate = 3f;
    [Tooltip("Secondes dans le noir avant que la plante tourne d'un cran.")]
    [SerializeField] private float plantInterval = 0.8f;

    [Header("Phare (formes provisoires)")]
    [SerializeField] private float towerHeight = 14f;
    [SerializeField] private float towerRadius = 1.5f;
    [SerializeField] private Material towerMaterial;
    [Tooltip("Vide = matériau du prefab récepteur (émissif).")]
    [SerializeField] private Material lampMaterial;
    [Tooltip("Layer de la tour. « Ground » bloque le joueur et la caméra.")]
    [SerializeField] private string towerLayer = "Ground";

    [Header("Fin")]
    [Tooltip("Un élément = un bloc, affiché seul, l'un après l'autre.")]
    [SerializeField] private string[] credits = { "SpacePuzzle", "Un jeu de …", "Merci d'avoir joué" };
    [Tooltip("Vide = on reste sur les crédits.")]
    [SerializeField] private string menuScene = "";

    [Header("Région")]
    [Tooltip("Vide = cherché automatiquement dans la scène.")]
    [SerializeField] private RegionLight regionLight;
    [SerializeField] private bool mirrorX;
    [Tooltip("Taille du puzzle (mètres par unité de grille).")]
    [SerializeField] private float spacing = 1.6f;

#if UNITY_EDITOR
    private const string RootName = "Puzzle_Phare";
    private const string ProfilePath = "Assets/FinDuJeu_Eblouissement.asset";
    private Transform root;
    private Vector3 center;
    private int groundMask;
    private BeamManager mgr;

    [ContextMenu("Orienter l'ancre sur la planète")]
    private void AlignAnchor()
    {
        BeamManager m = GetComponentInParent<BeamManager>();
        if (m == null || m.PlanetTransform == null) { Debug.LogWarning($"{name} : l'ancre doit être sous Crystal_Puzzle."); return; }
        Vector3 up = (transform.position - m.PlanetTransform.position).normalized;
        Vector3 fwd = Vector3.ProjectOnPlane(m.PlanetTransform.up, up);
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(Vector3.forward, up);
        Undo.RecordObject(transform, "Orienter l'ancre");
        transform.rotation = Quaternion.LookRotation(fwd.normalized, up);
    }

    [ContextMenu("Construire le Phare")]
    private void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning($"{name} : construction uniquement hors Play."); return; }

        foreach (GameObject p in new[] { emitterPrefab, relayPrefab, receiverPrefab })
            if (p == null || p.GetComponent<Crystal>() == null)
            {
                Debug.LogWarning($"{name} : assigne les prefabs Emitter, Relay et Receiver (avec Crystal à la racine).");
                return;
            }
        foreach (GameObject p in new[] { mixerPrefab, plantPrefab })
            if (p != null && p.GetComponent<Crystal>() == null)
            {
                Debug.LogWarning($"{name} : « {p.name} » n'a pas de composant Crystal à la racine.");
                return;
            }

        BeamManager manager = GetComponentInParent<BeamManager>();
        if (manager == null || manager.PlanetTransform == null)
        {
            Debug.LogWarning($"{name} : l'ancre doit être sous Crystal_Puzzle (BeamManager avec Planet).");
            return;
        }

        if (regionLight == null) regionLight = FindFirstObjectByType<RegionLight>();
        if (regionLight == null) { Debug.LogError($"{name} : aucun RegionLight dans la scène, les cristaux ne pourraient pas apparaître."); return; }
        if (chargeTime >= heatTime) { Debug.LogError($"{name} : Charge Time ({chargeTime:0.#}) doit être < Heat Time ({heatTime:0.#}), sinon le Phare ne peut pas s'allumer."); return; }

        mgr = manager;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Construire le Phare");
        int group = Undo.GetCurrentGroup();

        Transform old = transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject rootGo = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootGo, "Puzzle root");
        rootGo.transform.SetParent(transform, false);
        root = rootGo.transform;
        center = manager.PlanetTransform.position;
        groundMask = LayerMask.GetMask("Planete");

        // --- 1. Le Phare ---
        BuildTower(out Transform phareT, out Light lamp, out Renderer lampRenderer);
        Physics.SyncTransforms();

        // --- 2. Cristaux ---
        Crystal R = MakeCrystal(receiverPrefab, "Foyer_Phare", At(0f, 3f), CrystalType.Receiver, Colors.None, Colors.White, false, true);
        Crystal M = MakeCrystal(mixerPrefab != null ? mixerPrefab : relayPrefab, "Melangeur_Cyan", At(-4f, 9f), CrystalType.Mixer, Colors.None, Colors.None, false, true);
        Crystal H = MakeCrystal(relayPrefab, "Relais_Chaud", At(4f, 9f), CrystalType.Relay, Colors.None, Colors.None, true, true);
        Crystal P = MakeCrystal(plantPrefab != null ? plantPrefab : relayPrefab, "Plante_Phare", At(-9f, 13f), CrystalType.Relay, Colors.None, Colors.None, false, true);
        Crystal B = MakeCrystal(emitterPrefab, "Emetteur_Bleu_P", At(1f, 15f), CrystalType.Emitter, Colors.Blue, Colors.None, true, true);
        Crystal G = MakeCrystal(emitterPrefab, "Emetteur_Vert_P", At(-12f, 6f), CrystalType.Emitter, Colors.Green, Colors.None, true, true);
        Crystal E = MakeCrystal(emitterPrefab, "Emetteur_Rouge_P", At(9f, 14f), CrystalType.Emitter, Colors.Red, Colors.None, true, true);

        // --- 3. Liaisons (déplace le cristal si le relief ou un objet coupe le rayon) ---
        Link(M, R);
        Link(H, R);
        Link(P, M);
        Link(B, M);
        Link(G, P);
        Link(E, H);

        Crystal[] all = { R, M, H, P, B, G, E };
        Dictionary<Crystal, Quaternion> sol = new Dictionary<Crystal, Quaternion>();
        foreach (Crystal c in all) sol[c] = c.transform.rotation;

        // --- 4. Mécaniques ---
        AddHeat(H);
        AddPlant(P);

        Charge charge = Undo.AddComponent<Charge>(R.gameObject);
        SerializedObject sc = new SerializedObject(charge);
        Prop(sc, "chargeTime").floatValue = chargeTime;
        Prop(sc, "decayRate").floatValue = decayRate;
        SerializedProperty arr = Prop(sc, "stabilizeOnCharged");
        arr.arraySize = 1;
        arr.GetArrayElementAtIndex(0).objectReferenceValue = H.GetComponent<Surchauffe>();
        Prop(sc, "litColor").colorValue = new Color(1f, 1f, 0.92f);
        Prop(sc, "chargeLight").objectReferenceValue = MakeLight(R, "Lumiere_Charge", Color.white, 6f);
        sc.ApplyModifiedPropertiesWithoutUndo();

        // --- 5. Cinématique et fin ---
        List<MonoBehaviour> off = new List<MonoBehaviour>();
        PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
        CameraFollow cf = FindFirstObjectByType<CameraFollow>();
        PauseMenu pause = FindFirstObjectByType<PauseMenu>();
        if (pm != null) off.Add(pm); else Debug.LogWarning("Phare : PlayerMovement introuvable.");
        if (cf != null) off.Add(cf); else Debug.LogWarning("Phare : CameraFollow introuvable.");
        if (pause != null) off.Add(pause);

        Transform lensView = MakeView("Vue_Foyer", At(0f, 10f), 5f, Vector3.Lerp(R.transform.position, lamp.transform.position, 0.35f));
        Transform lampView = MakeView("Vue_Lanterne", At(0f, 8f), towerHeight * 0.75f, lamp.transform.position);

        CinematiquePhare cine = Undo.AddComponent<CinematiquePhare>(rootGo);
        SerializedObject so = new SerializedObject(cine);
        Prop(so, "regionLight").objectReferenceValue = regionLight;
        SerializedProperty ev = Prop(so, "eveils");
        ev.arraySize = 3;
        SetEveil(ev.GetArrayElementAtIndex(0), "_Lit_Rouge", E);
        SetEveil(ev.GetArrayElementAtIndex(1), "_Lit_Vert", G);
        SetEveil(ev.GetArrayElementAtIndex(2), "_Lit_Bleu", B);
        Prop(so, "phare").objectReferenceValue = phareT;
        Prop(so, "lensViewPoint").objectReferenceValue = lensView;
        Prop(so, "groundMask").intValue = groundMask;
        SetList(Prop(so, "disableDuring"), off);
        so.ApplyModifiedPropertiesWithoutUndo();

        BuildCanvas(out CanvasGroup white, out CanvasGroup[] blocks);
        Volume volume = BuildVolume();

        FinDuJeu fin = Undo.AddComponent<FinDuJeu>(rootGo);
        SerializedObject sf = new SerializedObject(fin);
        Prop(sf, "lampLight").objectReferenceValue = lamp;
        Prop(sf, "lampRenderer").objectReferenceValue = lampRenderer;
        Prop(sf, "viewPoint").objectReferenceValue = lampView;
        Prop(sf, "cinematique").objectReferenceValue = cine;
        Prop(sf, "glareVolume").objectReferenceValue = volume;
        Prop(sf, "white").objectReferenceValue = white;
        SerializedProperty cb = Prop(sf, "creditBlocks");
        cb.arraySize = blocks.Length;
        for (int i = 0; i < blocks.Length; i++) cb.GetArrayElementAtIndex(i).objectReferenceValue = blocks[i];
        Prop(sf, "menuScene").stringValue = menuScene;
        SetList(Prop(sf, "disableDuring"), off);
        sf.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(charge, "Fin du jeu");
        UnityEventTools.AddPersistentListener(charge.onCharged, fin.Play);
        EditorUtility.SetDirty(charge);

        EnablePostProcessing();

        // --- 6. Orientation de départ ---
        const int sE = 6, sH = 4, sB = -5, sG = 5, sP = 8;
        RotateSteps(E, sE);
        RotateSteps(H, sH);
        RotateSteps(B, sB);
        RotateSteps(G, sG);
        RotateSteps(P, sP);

        // --- 7. Vérification avec le vrai BeamManager ---
        int errors = 0;
        StringBuilder report = new StringBuilder();
        void Pose(Crystal c, int n) => c.transform.rotation = sol[c] * Quaternion.Euler(0f, n * StepOf(c), 0f);
        void PoseAll(int e, int h, int b, int g, int p) { Pose(E, e); Pose(H, h); Pose(B, b); Pose(G, g); Pose(P, p); Pose(M, 0); Pose(R, 0); }
        void Run() { Physics.SyncTransforms(); manager.Recompute(); }
        void Why(Crystal src, Crystal dst)
        {
            Reaches(src, 0, dst, out string why);
            report.AppendLine($"   ↳ rayon {src.name} → {dst.name} : {why}");
        }
        void Expect(bool ok, string msg, params Crystal[] pairs)
        {
            if (ok) return;
            errors++;
            report.AppendLine("✗ " + msg);
            for (int i = 0; i + 1 < pairs.Length; i += 2) Why(pairs[i], pairs[i + 1]);
        }

        typeof(BeamManager).GetMethod("RefreshCrystals", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(manager, null);

        // Départ
        PoseAll(sE, sH, sB, sG, sP);
        Run();
        Expect(!R.IsSatisfied, "Départ : Foyer_Phare est déjà blanc sans rien faire.");

        // Solution
        PoseAll(0, 0, 0, 0, 0);
        Run();
        Expect(P.OutputColor == Colors.Green, $"Plante_Phare ne relaie pas le vert (reçoit {P.ReceivedColor}).", G, P);
        Expect(M.OutputColor == Colors.Cyan, $"Melangeur_Cyan sort {M.OutputColor} au lieu de Cyan (reçoit {M.ReceivedColor}).", B, M, P, M);
        Expect(H.OutputColor == Colors.Red, $"Relais_Chaud ne relaie pas le rouge (reçoit {H.ReceivedColor}).", E, H);
        Expect(R.IsSatisfied, $"Foyer_Phare reçoit {R.ReceivedColor} au lieu de White.", M, R, H, R);
        string forced = ForcedOff(all);
        Expect(forced.Length == 0, $"Solution : cristaux éteints de force par le BeamManager (boucle instable) : {forced}.");

        // La plante dans une autre direction ne doit pas suffire
        for (int k = 1; k < Turn(P); k++)
        {
            Pose(P, k);
            Run();
            if (R.IsSatisfied) Expect(false, $"Plante_Phare au cran {k} : le foyer devient blanc quand même.");
            string f = ForcedOff(all);
            if (f.Length > 0) report.AppendLine($"⚠ Plante_Phare au cran {k} crée une boucle ({f}) : le BeamManager l'éteint, sans bloquer le puzzle.");
        }
        Pose(P, 0);
        Physics.SyncTransforms();

        // Raccourcis
        for (int k = 1; k < Turn(E); k++)
            if (TraceAt(E, k, R) == 0) report.AppendLine($"⚠ Raccourci : Emetteur_Rouge_P au cran {k} atteint Foyer_Phare directement (la surchauffe est contournée).");
        for (int k = 1; k < Turn(G); k++)
            if (TraceAt(G, k, M) == 0) report.AppendLine($"⚠ Raccourci : Emetteur_Vert_P au cran {k} atteint Melangeur_Cyan sans passer par la plante.");

        if (chargeTime > heatTime * 0.8f)
            report.AppendLine($"⚠ Charge Time ({chargeTime:0.#} s) proche de Heat Time ({heatTime:0.#} s) : très peu de marge pour le joueur.");

        // Retour à l'état de départ
        PoseAll(sE, sH, sB, sG, sP);
        Run();

        Undo.CollapseUndoOperations(group);

        // --- 8. Solution + rapport ---
        StringBuilder s = new StringBuilder();
        s.AppendLine("=== Solution du Phare — sens vus du dessus (horaire = menu Rotate) ===");
        s.AppendLine("Chaque émetteur apparaît quand sa région est allumée.");
        s.AppendLine($"1. Emetteur_Bleu_P : {Moves(-sB, StepOf(B))} → vise Melangeur_Cyan.");
        s.AppendLine($"2. Emetteur_Vert_P : {Moves(-sG, StepOf(G))} → vise Plante_Phare. La plante tourne seule dans le noir : " +
                     "si elle est éclairée dans la mauvaise direction, détourne le vert d'un cran le temps qu'elle pointe vers le mélangeur.");
        s.AppendLine("   → Melangeur_Cyan reçoit bleu + vert et sort du cyan vers Foyer_Phare.");
        s.AppendLine($"3. Relais_Chaud : {Moves(-sH, StepOf(H))} → vise Foyer_Phare.");
        s.AppendLine($"4. EN DERNIER, Emetteur_Rouge_P : {Moves(-sE, StepOf(E))} → vise Relais_Chaud. Foyer blanc : tenir {chargeTime:0.#} s avant que le relais surchauffe ({heatTime:0.#} s).");
        s.AppendLine("→ Le Phare s'allume : éblouissement, crédits, fin.");
        Debug.Log(s.ToString());

        if (errors == 0) Debug.Log($"Vérification Phare : OK, la solution fonctionne avec le BeamManager.\n{report}");
        else Debug.LogError($"Vérification Phare : {errors} problème(s).\n{report}");
    }

    // ---------- Construction ----------

    private void Link(Crystal mover, Crystal target)
    {
        float[] angles = { 0f, 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f, 90f, -90f };
        float[] dists = { 1f, 0.85f, 1.15f, 0.7f, 1.3f };
        FixLink(mover, target, angles, dists,
                () => AimExact(mover, target.transform.position, 0),
                () => Reaches(mover, 0, target, out _),
                mover.name);
    }

    private void BuildTower(out Transform phareT, out Light lamp, out Renderer lampRenderer)
    {
        GameObject phare = new GameObject("Phare");
        Undo.RegisterCreatedObjectUndo(phare, "Phare");
        phare.transform.SetParent(root, false);
        Vector3 pos = At(0f, 0f);
        Vector3 up = Radial(pos);
        phare.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.ProjectOnPlane(root.forward, up).normalized, up));
        phareT = phare.transform;

        GameObject tour = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tour.name = "Tour";
        tour.transform.SetParent(phare.transform, false);
        tour.transform.localPosition = new Vector3(0f, towerHeight * 0.5f, 0f);
        tour.transform.localScale = new Vector3(towerRadius * 2f, towerHeight * 0.5f, towerRadius * 2f);
        if (towerMaterial != null) tour.GetComponent<MeshRenderer>().sharedMaterial = towerMaterial;
        int layer = LayerMask.NameToLayer(towerLayer);
        if (layer < 0) Debug.LogWarning($"{name} : layer « {towerLayer} » introuvable, la tour reste sur Default (le joueur la traverse).");
        else if (layer == LayerMask.NameToLayer("Planete")) Debug.LogWarning($"{name} : Tower Layer ne doit pas être Planete.");
        else tour.layer = layer;

        float lampY = towerHeight + towerRadius * 0.9f;
        GameObject lanterne = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lanterne.name = "Lanterne";
        DestroyImmediate(lanterne.GetComponent<Collider>());
        lanterne.transform.SetParent(phare.transform, false);
        lanterne.transform.localPosition = new Vector3(0f, lampY, 0f);
        lanterne.transform.localScale = Vector3.one * towerRadius * 2.2f;
        Material mat = lampMaterial;
        if (mat == null)
        {
            Renderer rr = receiverPrefab.GetComponentInChildren<Renderer>();
            if (rr != null) mat = rr.sharedMaterial;
        }
        if (mat != null) lanterne.GetComponent<MeshRenderer>().sharedMaterial = mat;
        lampRenderer = lanterne.GetComponent<MeshRenderer>();

        GameObject lg = new GameObject("Lumiere_Phare");
        lg.transform.SetParent(phare.transform, false);
        lg.transform.localPosition = new Vector3(0f, lampY, 0f);
        lamp = lg.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = 80f;
        lamp.intensity = 0f;
        lamp.color = new Color(1f, 0.97f, 0.88f);

        Ground(phare.transform, 0.3f);
    }

    private void AddHeat(Crystal c)
    {
        Surchauffe h = Undo.AddComponent<Surchauffe>(c.gameObject);
        SerializedObject sh = new SerializedObject(h);
        Prop(sh, "heatTime").floatValue = heatTime;
        Prop(sh, "coolRate").floatValue = coolRate;
        Prop(sh, "heatLight").objectReferenceValue = MakeLight(c, "Lumiere_Chaleur", new Color(1f, 0.5f, 0.1f), 5f);
        sh.ApplyModifiedPropertiesWithoutUndo();
    }

    private void AddPlant(Crystal c)
    {
        Phototropisme photo = c.GetComponent<Phototropisme>();
        if (photo == null) photo = Undo.AddComponent<Phototropisme>(c.gameObject);
        SerializedObject sp = new SerializedObject(photo);
        Prop(sp, "interval").floatValue = plantInterval;
        Prop(sp, "direction").intValue = 1;
        SerializedProperty visual = Prop(sp, "visual");
        if (visual.objectReferenceValue == null)
        {
            Renderer vr = c.GetComponentInChildren<Renderer>();
            if (vr != null && vr.transform != c.transform) visual.objectReferenceValue = vr.transform;
        }
        sp.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Light MakeLight(Crystal c, string objName, Color color, float range)
    {
        GameObject go = new GameObject(objName);
        Undo.RegisterCreatedObjectUndo(go, objName);
        go.transform.SetParent(c.transform, false);
        go.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = range;
        l.intensity = 0f;
        l.color = color;
        return l;
    }

    private Transform MakeView(string objName, Vector3 groundPos, float height, Vector3 lookAt)
    {
        Vector3 pos = groundPos + Radial(groundPos) * height;
        GameObject go = new GameObject(objName);
        Undo.RegisterCreatedObjectUndo(go, objName);
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(lookAt - pos, Radial(pos)));
        return go.transform;
    }

    private static void SetEveil(SerializedProperty e, string property, Crystal c)
    {
        e.FindPropertyRelative("regionProperty").stringValue = property;
        e.FindPropertyRelative("crystal").objectReferenceValue = c;
    }

    private static void SetList(SerializedProperty p, List<MonoBehaviour> list)
    {
        p.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
    }

    private void BuildCanvas(out CanvasGroup white, out CanvasGroup[] blocks)
    {
        GameObject cv = new GameObject("Canvas_Fin", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(cv, "Canvas_Fin");
        cv.transform.SetParent(root, false);
        Canvas canvas = cv.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = cv.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject bl = new GameObject("Blanc", typeof(RectTransform));
        bl.transform.SetParent(cv.transform, false);
        RectTransform brt = (RectTransform)bl.transform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        Image img = bl.AddComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = false;
        white = bl.AddComponent<CanvasGroup>();
        white.alpha = 0f;
        white.blocksRaycasts = false;
        white.interactable = false;

        // Tous les blocs au centre : ils s'affichent un par un, au même endroit.
        int n = credits != null ? credits.Length : 0;
        blocks = new CanvasGroup[n];
        for (int i = 0; i < n; i++)
        {
            GameObject t = new GameObject($"Credit_{i + 1}", typeof(RectTransform));
            t.transform.SetParent(cv.transform, false);
            RectTransform rt = (RectTransform)t.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1600f, 600f);
            rt.anchoredPosition = Vector2.zero;
            TextMeshProUGUI tmp = t.AddComponent<TextMeshProUGUI>();
            tmp.text = credits[i];
            tmp.fontSize = i == 0 ? 96f : 56f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.1f, 0.12f, 0.2f);
            tmp.raycastTarget = false;
            CanvasGroup g = t.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            g.blocksRaycasts = false;
            g.interactable = false;
            blocks[i] = g;
        }
    }

    private Volume BuildVolume()
    {
        VolumeProfile prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (prof == null)
        {
            prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, ProfilePath);
            Bloom bloom = prof.Add<Bloom>(false);
            bloom.intensity.Override(12f);
            bloom.threshold.Override(0.6f);
            bloom.scatter.Override(0.8f);
            ColorAdjustments ca = prof.Add<ColorAdjustments>(false);
            ca.postExposure.Override(6f);
            foreach (VolumeComponent comp in prof.components) AssetDatabase.AddObjectToAsset(comp, prof);
            EditorUtility.SetDirty(prof);
            AssetDatabase.SaveAssets();
            Debug.Log($"Phare : profil d'éblouissement créé ({ProfilePath}).");
        }

        GameObject go = new GameObject("Volume_Eblouissement");
        Undo.RegisterCreatedObjectUndo(go, "Volume");
        go.transform.SetParent(root, false);
        Volume v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = 100f;
        v.weight = 0f;
        v.sharedProfile = prof;
        return v;
    }

    private static void EnablePostProcessing()
    {
        Camera cam = Camera.main;
        if (cam == null) { Debug.LogWarning("Phare : aucune caméra taguée MainCamera, active Post Processing à la main."); return; }
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (data == null || data.renderPostProcessing) return;
        Undo.RecordObject(data, "Post Processing");
        data.renderPostProcessing = true;
        EditorUtility.SetDirty(data);
        Debug.Log("Phare : Post Processing activé sur la Main Camera.");
    }

    // ---------- Relief ----------

    private void FixLink(Crystal mover, Crystal pivot, float[] angles, float[] dists,
                         System.Action reaim, System.Func<bool> ok, string label)
    {
        reaim();
        Physics.SyncTransforms();
        if (ok()) return;

        Undo.RecordObject(mover.transform, "Relief");
        Vector3 pos0 = mover.transform.position;
        Quaternion rot0 = mover.transform.rotation;
        Vector3 piv = pivot.transform.position;
        Vector3 axis = Radial(piv);

        foreach (float f in dists)
            foreach (float a in angles)
            {
                if (f == 1f && a == 0f) continue;
                Vector3 p = center + Quaternion.AngleAxis(a, axis) * (pos0 - center);
                if (f != 1f) p = ArcPointU(piv, p, f);

                mover.transform.SetPositionAndRotation(p, rot0);
                mover.AlignToPlanet(false);
                reaim();
                Physics.SyncTransforms();
                if (ok())
                {
                    Debug.Log($"Relief : {label} déplacé ({a:+0;-0;0}° autour de {pivot.name}, distance ×{f:0.00}).");
                    return;
                }
            }

        mover.transform.SetPositionAndRotation(pos0, rot0);
        mover.AlignToPlanet(false);
        reaim();
        Physics.SyncTransforms();
        Reaches(mover, 0, pivot, out string why);
        Debug.LogWarning($"Relief : aucune position trouvée pour {label} ({why}). Déplace l'ancre et reconstruis.");
    }

    // ---------- Traceur (règles de BeamManager.TraceBeam) ----------

    private static T Field<T>(object o, string fieldName, T def)
    {
        FieldInfo f = o.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return def;
        object v = f.GetValue(o);
        if (v is LayerMask lm && typeof(T) == typeof(int)) return (T)(object)lm.value;
        return v is T t ? t : def;
    }

    private float GroundRadius(Vector3 dir, int mask, float R)
    {
        if (Physics.Raycast(center + dir * (R + 50f), -dir, out RaycastHit hit, R + 100f, mask))
            return (hit.point - center).magnitude;
        return -1f;
    }

    // 0 = atteint dst, 1 = bloqué par un objet, 2 = coupé par une pente, 3 = rien touché.
    private int TraceCore(Crystal src, int output, Crystal dst, float limit, out string why)
    {
        int beamMask = Field(mgr, "beamMask", ~0);
        int gMask = Field(mgr, "groundMask", groundMask);
        if (gMask == 0) gMask = groundMask;
        float maxDist = Field(mgr, "maxDistance", 50f);
        float step = Mathf.Max(0.05f, Field(mgr, "stepLength", 0.4f));
        float maxSlope = Field(mgr, "maxSlopeAngle", 35f);
        int mask = beamMask & ~gMask;
        limit = Mathf.Min(limit, maxDist);

        Vector3 p = src.BeamStart();
        Vector3 r = Radial(p);
        Vector3 dir = Vector3.ProjectOnPlane(Heading(src, output), r).normalized;
        string tail = "";
        if (dst != null)
        {
            float aim = Vector3.SignedAngle(dir, Vector3.ProjectOnPlane(dst.transform.position - p, r), r);
            tail = $" (cible à {ArcLength(p, dst.transform.position):0.0} m, écart de visée {aim:0.0}°)";
        }

        float startR = (p - center).magnitude;
        float g = GroundRadius(r, gMask, startR);
        if (g < 0f) g = startR;
        float height = startR - g;

        float travelled = 0f;
        Vector3 prev = p;
        while (travelled < limit)
        {
            float R = (prev - center).magnitude;
            Quaternion q = Quaternion.AngleAxis(step / R * Mathf.Rad2Deg, Vector3.Cross(r, dir));
            r = (q * r).normalized;
            dir = Vector3.ProjectOnPlane(q * dir, r).normalized;

            float ng = GroundRadius(r, gMask, R);
            if (ng < 0f) ng = g;
            float slope = Mathf.Atan2(Mathf.Abs(ng - g), step) * Mathf.Rad2Deg;
            if (slope > maxSlope)
            {
                why = $"coupé par une pente de {slope:0}° à {travelled:0.0} m{tail}.";
                return 2;
            }
            g = ng;

            Vector3 next = center + r * (g + height);
            Vector3 seg = next - prev;
            RaycastHit[] hits = Physics.RaycastAll(prev, seg.normalized, seg.magnitude, mask);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                Crystal c = h.collider.GetComponentInParent<Crystal>();
                if (c == src) continue;
                float at = travelled + h.distance;
                if (dst != null && c == dst) { why = $"atteint à {at:0.0} m (la géométrie est bonne)."; return 0; }
                string what = c != null ? $"le cristal {c.name}" : $"l'objet « {h.collider.name} » (layer {LayerMask.LayerToName(h.collider.gameObject.layer)})";
                why = $"bloqué par {what} à {at:0.0} m{tail}.";
                return 1;
            }
            travelled += seg.magnitude;
            prev = next;
        }
        why = dst != null ? $"rien touché sur {limit:0} m : cible ratée{tail}." : "libre.";
        return 3;
    }

    private bool Reaches(Crystal src, int output, Crystal dst, out string why)
        => TraceCore(src, output, dst, float.MaxValue, out why) == 0;

    private int TraceAt(Crystal src, int steps, Crystal dst)
    {
        Quaternion keep = src.transform.rotation;
        src.transform.rotation = keep * Quaternion.Euler(0f, steps * StepOf(src), 0f);
        int r = TraceCore(src, 0, dst, float.MaxValue, out _);
        src.transform.rotation = keep;
        return r;
    }

    private static string ForcedOff(Crystal[] all)
    {
        FieldInfo f = typeof(Crystal).GetField("forcedOff", BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return "";
        List<string> names = new List<string>();
        foreach (Crystal c in all) if ((bool)f.GetValue(c)) names.Add(c.name);
        return string.Join(", ", names);
    }

    // ---------- Placement ----------

    private Vector3 At(float x, float z) => AtM(x * spacing, z * spacing);

    // Mètres mesurés SUR la surface depuis l'ancre (suit la courbure de la planète).
    private Vector3 AtM(float x, float z)
    {
        Vector3 r0 = root.position - center;
        float R0 = r0.magnitude;
        Vector3 t = root.right * (mirrorX ? -x : x) + root.forward * z;
        t = Vector3.ProjectOnPlane(t, r0.normalized);
        float d = t.magnitude;
        if (d < 1e-4f) return root.position;
        float th = d / R0;
        return center + (r0.normalized * Mathf.Cos(th) + t / d * Mathf.Sin(th)) * R0;
    }

    private Vector3 Radial(Vector3 p) => (p - center).normalized;
    private static int Turn(Crystal c) => Mathf.Max(1, Mathf.RoundToInt(360f / StepOf(c)));

    private static SerializedProperty Prop(SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) Debug.LogError($"Champ « {field} » introuvable sur {so.targetObject.name}.");
        return p;
    }

    private Crystal MakeCrystal(GameObject prefab, string objName, Vector3 worldPos, CrystalType type,
                                Colors emitted, Colors target, bool rotatable, bool countsForVictory)
    {
        GameObject go = PrefabUtility.IsPartOfPrefabAsset(prefab)
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, root)
            : Instantiate(prefab, root);
        Undo.RegisterCreatedObjectUndo(go, objName);
        go.name = objName;
        go.SetActive(true);
        go.transform.SetPositionAndRotation(worldPos, root.rotation);

        Crystal c = go.GetComponent<Crystal>();
        SerializedObject so = new SerializedObject(c);
        Prop(so, "crystalType").intValue = (int)type;
        Prop(so, "emittedColor").intValue = (int)emitted;
        Prop(so, "targetColor").intValue = (int)target;
        Prop(so, "rotatable").boolValue = rotatable;
        Prop(so, "countsForVictory").boolValue = countsForVictory;
        Prop(so, "startStep").intValue = 0;
        Prop(so, "stepAngle").floatValue = 15f;
        Prop(so, "hintColor").intValue = (int)Colors.None;
        Prop(so, "firstTarget").objectReferenceValue = null;
        Prop(so, "secondTarget").objectReferenceValue = null;
        Prop(so, "aimTarget").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();

        c.AlignToPlanet(false);
        return c;
    }

    private static float StepOf(Crystal c) => new SerializedObject(c).FindProperty("stepAngle").floatValue;

    private static Vector3 Heading(Crystal c, int index)
    {
        Vector3[] h = c.GetHeadings();
        return h[Mathf.Clamp(index, 0, h.Length - 1)];
    }

    private static void RotateDeg(Crystal c, float deg)
    {
        Undo.RecordObject(c.transform, "Rotate");
        c.transform.rotation = c.transform.rotation * Quaternion.Euler(0f, deg, 0f);   // comme Crystal.SetYaw
    }

    private static void RotateSteps(Crystal c, int steps) => RotateDeg(c, steps * StepOf(c));

    private void AimExact(Crystal c, Vector3 target, int index)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = c.BeamStart();
            Vector3 r = Radial(p);
            Vector3 h = Vector3.ProjectOnPlane(Heading(c, index), r);
            Vector3 d = Vector3.ProjectOnPlane(target - p, r);
            if (h.sqrMagnitude < 1e-6f || d.sqrMagnitude < 1e-6f) return;
            float a = Vector3.SignedAngle(h, d, r);
            if (Mathf.Abs(a) < 0.01f) return;
            RotateDeg(c, a);
        }
    }

    private Vector3 ArcPointU(Vector3 a, Vector3 b, float f)
    {
        Vector3 ra = a - center, rb = b - center;
        return center + Vector3.SlerpUnclamped(ra.normalized, rb.normalized, f) * Mathf.LerpUnclamped(ra.magnitude, rb.magnitude, f);
    }

    private float ArcLength(Vector3 a, Vector3 b)
    {
        Vector3 ra = a - center, rb = b - center;
        return Vector3.Angle(ra, rb) * Mathf.Deg2Rad * (ra.magnitude + rb.magnitude) * 0.5f;
    }

    private static string Moves(int n, float step)
    {
        int full = Mathf.RoundToInt(360f / step);
        n = ((n % full) + full) % full;
        if (n > full / 2) n -= full;
        if (n == 0) return "ne pas toucher";
        int a = Mathf.Abs(n);
        return $"{a} cran{(a > 1 ? "s" : "")} {(n > 0 ? "horaire" : "anti-horaire")}";
    }

    // Pose la base de l'objet sur le point de sol le plus bas sous son emprise, puis l'enfonce de « sink ».
    private void Ground(Transform t, float sink)
    {
        Vector3 up = Radial(t.position);

        float minAlong = 0f, maxLat = 0.5f;
        bool any = false;
        foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 d = mf.transform.TransformPoint(corner) - t.position;
                float along = Vector3.Dot(d, up);
                if (!any || along < minAlong) minAlong = along;
                maxLat = Mathf.Max(maxLat, (d - up * along).magnitude);
                any = true;
            }
        }

        float ground = float.MaxValue;
        Vector3 tangent = Vector3.ProjectOnPlane(t.forward, up).normalized;
        for (int i = 0; i <= 8; i++)
        {
            Vector3 p = t.position;
            if (i > 0) p += Quaternion.AngleAxis(i * 45f, up) * tangent * (maxLat * 0.6f);
            Vector3 dir = Radial(p);
            float r = (p - center).magnitude;
            if (Physics.Raycast(center + dir * (r + 50f), -dir, out RaycastHit hit, r + 100f, groundMask))
                ground = Mathf.Min(ground, (hit.point - center).magnitude);
        }

        if (ground == float.MaxValue)
        {
            Debug.LogWarning($"{t.name} : sol introuvable sous l'objet.");
            return;
        }

        Undo.RecordObject(t, "Ground");
        t.position = center + up * (ground - sink - minAlong);
    }
#endif
}