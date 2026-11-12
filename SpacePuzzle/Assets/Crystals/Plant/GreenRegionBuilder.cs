using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif

// Construit en un clic (hors Play) le puzzle de la région Verte sous cette ancre, puis le VÉRIFIE
// avec le vrai BeamManager. Plantes phototropes, pont-liane qui pousse (layer Planete = sol pour les rayons),
// troncs qui ferment les raccourcis, jungle décorative hors des rayons.
// Le récepteur final est JAUNE : il faut le vert de la chaîne + le rouge d'Emitter_Rouge_V.
// Coordonnées locales : X sur le côté, Z vers le pôle (le Phare), Y vers l'extérieur.
public class GreenRegionBuilder : MonoBehaviour
{
    [Header("Prefabs (composant Crystal à la racine)")]
    [SerializeField] private GameObject emitterPrefab;
    [SerializeField] private GameObject relayPrefab;
    [SerializeField] private GameObject receiverPrefab;
    [Tooltip("Vide = prefab Relay.")]
    [SerializeField] private GameObject plantPrefab;

    [Header("Plantes")]
    [Tooltip("Secondes sans lumière avant chaque cran.")]
    [SerializeField] private float plantInterval = 3f;
    [Tooltip("Degrés par seconde pendant un cran de 45°.")]
    [SerializeField] private float plantRotationSpeed = 90f;

    [Header("Décor")]
    [SerializeField] private Material rockMaterial;
    [SerializeField] private Material vineMaterial;
    [SerializeField] private Material trunkMaterial;
    [SerializeField] private Material mushroomMaterial;
    [Tooltip("Vide = cylindre simple. Le tronc doit garder un collider d'au moins 1,2 m de diamètre.")]
    [SerializeField] private GameObject treePrefab;
    [Tooltip("Vide = champignon simple.")]
    [SerializeField] private GameObject mushroomPrefab;
    [Tooltip("Échelle aléatoire min / max des arbres et champignons.")]
    [SerializeField] private Vector2 decorScale = new Vector2(0.85f, 1.3f);
    [Tooltip("Layer des troncs et champignons : coché dans le Beam Mask, différent de Planete.")]
    [SerializeField] private string obstacleLayer = "Default";
    [SerializeField] private float obstacleSink = 0.4f;
    [SerializeField] private float ridgeHeight = 3f;
    [SerializeField] private float ridgeLength = 10f;
    [SerializeField] private int jungleCount = 10;
    [SerializeField] private int seed = 7;

    [Header("Région")]
    [Tooltip("Vide = cherché automatiquement dans la scène.")]
    [SerializeField] private RegionLight regionLight;
    [SerializeField] private string regionProperty = "_Lit_Vert";
    [Tooltip("Coché : le puzzle s'étend vers +X au lieu de -X.")]
    [SerializeField] private bool mirrorX;
    [SerializeField] private float spacing = 1.2f;

#if UNITY_EDITOR
    private const string RootName = "Puzzle_Vert";
    private Transform root;
    private Vector3 center;
    private int groundMask;
    private BeamManager mgr;

    private struct Beam { public Vector3 a, b; public string label; }
    private struct Shortcut { public Crystal src, dst; public int steps; public bool bridgeOn; }
    private readonly List<Beam> beams = new List<Beam>();
    private readonly List<Crystal> placed = new List<Crystal>();
    private readonly List<GameObject> decor = new List<GameObject>();

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

    [ContextMenu("Construire le puzzle")]
    private void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning($"{name} : construction uniquement hors Play."); return; }

        GameObject plantSrc = plantPrefab != null ? plantPrefab : relayPrefab;
        foreach (GameObject p in new[] { emitterPrefab, relayPrefab, receiverPrefab, plantSrc })
            if (p == null || p.GetComponent<Crystal>() == null)
            {
                Debug.LogWarning($"{name} : assigne les prefabs Emitter, Relay et Receiver (avec Crystal à la racine).");
                return;
            }

        BeamManager manager = GetComponentInParent<BeamManager>();
        if (manager == null || manager.PlanetTransform == null)
        {
            Debug.LogWarning($"{name} : l'ancre doit être sous Crystal_Puzzle (BeamManager avec Planet).");
            return;
        }
        mgr = manager;
        if (regionLight == null) regionLight = FindFirstObjectByType<RegionLight>();

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Construire la région Verte");
        int group = Undo.GetCurrentGroup();

        Transform old = transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject rootGo = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootGo, "Puzzle root");
        rootGo.transform.SetParent(transform, false);
        root = rootGo.transform;
        center = manager.PlanetTransform.position;
        groundMask = LayerMask.GetMask("Planete");
        beams.Clear();
        placed.Clear();
        decor.Clear();

        // --- 1. Cristaux ---
        Crystal emitter  = MakeCrystal(emitterPrefab,  "Emitter_Vert",    At(4f, -9f),   CrystalType.Emitter,  Colors.Green, Colors.None,   true,  true);
        Crystal plant1   = MakePlant(plantSrc,         "Plante_1",        At(0f, 0f),    1);
        Crystal relayV   = MakeCrystal(relayPrefab,    "Relay_V",         At(-13f, 1f),  CrystalType.Relay,    Colors.None,  Colors.None,   true,  true);
        Crystal plant2   = MakePlant(plantSrc,         "Plante_2",        At(-16f, 10f), -1);
        Crystal receiver = MakeCrystal(receiverPrefab, "Receiver_Vert",   At(-24f, 14f), CrystalType.Receiver, Colors.None,  Colors.Yellow, false, true);
        Crystal emitterR = MakeCrystal(emitterPrefab,  "Emitter_Rouge_V", At(-27f, 3f),  CrystalType.Emitter,  Colors.Red,   Colors.None,   true,  true);
        placed.AddRange(new[] { emitter, plant1, relayV, plant2, receiver, emitterR });

        // --- 2. Ravin : naturel si une pente coupe déjà Plante_1 → Relay_V, sinon arête rocheuse ---
        AimExact(plant1, relayV.transform.position, 0);
        Physics.SyncTransforms();
        int natural = TraceCore(plant1, 0, relayV, float.MaxValue, out string natWhy);
        GameObject ridge = null;
        if (natural == 2) Debug.Log($"Ravin naturel entre Plante_1 et Relay_V ({natWhy}) : pas d'arête ajoutée.");
        else
        {
            if (natural == 1) Debug.LogWarning($"Plante_1 → Relay_V : {natWhy}");
            ridge = MakeRidge(plant1.BeamStart(), relayV.transform.position);
        }

        // --- 3. Relief et anti-retour ---
        float[] dists = { 1f, 0.85f, 1.15f, 0.7f, 1.3f };
        FixLink(emitter, plant1, new[] { 0f, 10f, -10f, 20f, -20f, 30f, -30f, 45f, -45f, 60f, -60f }, dists, ridge,
                () => AimExact(emitter, plant1.transform.position, 0), () => Reaches(emitter, 0, plant1, out _), "Emitter_Vert");

        AimExact(relayV, plant2.transform.position, 0);
        MoveAimTarget(relayV, 0, plant2, plant1);   // Relay_V ne peut jamais renvoyer vers Plante_1
        FixLink(plant2, relayV, new[] { 0f, 15f, -15f, 30f, -30f, 45f, -45f }, dists, ridge,
                () => AimExact(relayV, plant2.transform.position, 0), () => Reaches(relayV, 0, plant2, out _), "Plante_2");

        AimExact(plant2, receiver.transform.position, 0);
        MoveAimTarget(plant2, 0, receiver, relayV); // Plante_2 ne peut jamais renvoyer vers Relay_V
        FixLink(receiver, plant2, new[] { 0f, 45f, -45f, 90f, -90f }, dists, ridge,
                () => AimExact(plant2, receiver.transform.position, 0), () => Reaches(plant2, 0, receiver, out _), "Receiver_Vert");

        FixLink(emitterR, receiver, new[] { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 90f, -90f }, dists, ridge,
                () => AimExact(emitterR, receiver.transform.position, 0), () => Reaches(emitterR, 0, receiver, out _), "Emitter_Rouge_V");

        AddBeam(emitter, plant1); AddBeam(plant1, relayV); AddBeam(relayV, plant2); AddBeam(plant2, receiver);
        AddBeam(emitterR, receiver);

        // --- 4. Graine (dans un autre cran de Plante_1, sur un trajet libre) ---
        Crystal graine = MakeSeed(plant1, out int gStep);

        // --- 5. Pont-liane au-dessus du ravin ---
        GameObject bridge = MakeBridge(plant1, relayV);
        if (bridge == null) Debug.LogError("Pont : aucun relief à franchir entre Plante_1 et Relay_V.");

        Crystal[] all = { emitter, plant1, relayV, plant2, receiver, graine, emitterR };
        Dictionary<Crystal, Quaternion> sol = new Dictionary<Crystal, Quaternion>();
        foreach (Crystal c in new[] { emitter, plant1, relayV, plant2, emitterR }) sol[c] = c.transform.rotation;

        // --- 6. Troncs sur les raccourcis, puis jungle décorative ---
        int trunks = BlockShortcuts(bridge, emitter, plant1, relayV, graine, plant2, receiver, gStep, emitterR);
        if (trunks > 0) Debug.Log($"Jungle : {trunks} tronc(s) posé(s) pour fermer des raccourcis.");
        MakeJungle();

        // --- 7. Croissance et allumage ---
        ReceiverWatcher seedWatch = Watch(graine);
        if (bridge != null)
        {
            Croissance grow = Undo.AddComponent<Croissance>(bridge);
            Undo.RecordObject(seedWatch, "Croissance");
            UnityEventTools.AddVoidPersistentListener(seedWatch.onLit, grow.Grow);
            EditorUtility.SetDirty(seedWatch);
        }

        ReceiverWatcher final = Watch(receiver);
        if (regionLight != null)
        {
            Undo.RecordObject(final, "Region light");
            UnityEventTools.AddStringPersistentListener(final.onLit, regionLight.LightRegion, regionProperty);
            EditorUtility.SetDirty(final);
        }
        else Debug.LogWarning($"{name} : aucun RegionLight trouvé, branche On Lit de Receiver_Vert à la main.");

        // --- 8. Orientation de départ ---
        const int sE = -3, sR = 4, sP1 = 3, sP2 = -2, sRd = 3;
        RotateSteps(emitter, sE);
        RotateSteps(relayV, sR);
        RotateSteps(plant1, sP1);
        RotateSteps(plant2, sP2);
        RotateSteps(emitterR, sRd);

        // --- 9. Vérification avec le vrai BeamManager ---
        int errors = 0;
        StringBuilder report = new StringBuilder();
        void Pose(Crystal c, int n) => c.transform.rotation = sol[c] * Quaternion.Euler(0f, n * StepOf(c), 0f);
        void Run() { Physics.SyncTransforms(); manager.Recompute(); }
        void Bridge(bool on) { if (bridge != null) bridge.SetActive(on); }
        void Expect(bool ok, string msg) { if (ok) return; errors++; report.AppendLine("✗ " + msg); }
        void ExpectBeam(bool ok, string msg, Crystal src, Crystal dst)
        {
            if (ok) return;
            errors++;
            report.AppendLine("✗ " + msg);
            Reaches(src, 0, dst, out string why);
            report.AppendLine($"   ↳ rayon {src.name} → {dst.name} : {why}");
        }

        typeof(BeamManager).GetMethod("RefreshCrystals", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(manager, null);

        // Départ
        Bridge(false);
        Pose(emitter, sE); Pose(plant1, sP1); Pose(relayV, sR); Pose(plant2, sP2); Pose(emitterR, sRd);
        Run();
        Expect(!graine.IsSatisfied && !receiver.IsSatisfied, "Départ : un récepteur est déjà allumé sans rien faire.");

        // Étape 1 : Plante_1 vers la graine (rouge encore écarté)
        Pose(emitter, 0); Pose(plant1, gStep); Pose(relayV, 0); Pose(plant2, 0); Pose(emitterR, sRd);
        Run();
        ExpectBeam(plant1.OutputColor == Colors.Green, $"Étape 1 : Plante_1 ne relaie pas (reçoit {plant1.ReceivedColor}).", emitter, plant1);
        ExpectBeam(graine.IsSatisfied, $"Étape 1 : Graine_1 reçoit {graine.ReceivedColor} au lieu de Green.", plant1, graine);

        // Étape 2 : sans pont, le ravin doit couper le rayon
        Pose(plant1, 0);
        Run();
        Expect(relayV.ReceivedColor == Colors.None, "Sans le pont, Relay_V reçoit déjà le vert : le ravin ne coupe pas le rayon.");

        // Étape 3 : avec le pont ; d'abord le rouge seul, puis rouge + vert
        Expect(bridge != null, "Pas de pont construit.");
        Bridge(true);
        Pose(emitter, sE); Pose(emitterR, 0);
        Run();
        Expect(!receiver.IsSatisfied, "Rouge seul : Receiver_Vert s'allume déjà sans le vert.");
        Pose(emitter, 0);
        Run();
        ExpectBeam(plant1.OutputColor == Colors.Green, $"Final : Plante_1 ne relaie pas (reçoit {plant1.ReceivedColor}).", emitter, plant1);
        ExpectBeam(relayV.OutputColor == Colors.Green, $"Final : Relay_V ne relaie pas (reçoit {relayV.ReceivedColor}).", plant1, relayV);
        ExpectBeam(plant2.OutputColor == Colors.Green, $"Final : Plante_2 ne relaie pas (reçoit {plant2.ReceivedColor}).", relayV, plant2);
        ExpectBeam(receiver.IsSatisfied, $"Final : Receiver_Vert reçoit {receiver.ReceivedColor} au lieu de Yellow.", plant2, receiver);
        ExpectBeam(Reaches(emitterR, 0, receiver, out _), "Final : le rayon rouge n'atteint pas Receiver_Vert.", emitterR, receiver);
        string forced = ForcedOff(all);
        Expect(forced.Length == 0, $"Final : cristaux éteints de force par le BeamManager (boucle instable) : {forced}.");

        // Boucles : tour complet de la vanne et de Plante_2
        bool p1On = plant1.OutputColor == Colors.Green, rvOn = relayV.OutputColor == Colors.Green;
        for (int k = 0; k < Turn(relayV); k++)
        {
            Pose(relayV, k); Run();
            if (p1On && plant1.OutputColor != Colors.Green) Expect(false, $"Boucle : Relay_V au cran {k} éteint Plante_1.");
        }
        Pose(relayV, 0);
        for (int k = 0; k < Turn(plant2); k++)
        {
            Pose(plant2, k); Run();
            string off = "";
            if (p1On && plant1.OutputColor != Colors.Green) off += " Plante_1";
            if (rvOn && relayV.OutputColor != Colors.Green) off += " Relay_V";
            if (off.Length > 0) report.AppendLine($"⚠ Plante_2 au cran {k} éteint{off} (elle repartira seule, sans bloquer le puzzle).");
        }
        Pose(plant2, 0);

        // Raccourcis restants
        foreach (Shortcut s in FindShortcuts(bridge, emitter, plant1, relayV, graine, plant2, receiver, gStep, emitterR))
            report.AppendLine($"⚠ Raccourci : {s.src.name} au cran {s.steps} atteint {s.dst.name} directement{(s.bridgeOn ? " (pont poussé)" : "")}.");

        // Retour à l'état de départ
        Bridge(false);
        Pose(emitter, sE); Pose(plant1, sP1); Pose(relayV, sR); Pose(plant2, sP2); Pose(emitterR, sRd);
        Run();

        Undo.CollapseUndoOperations(group);

        // --- 10. Solution + rapport ---
        StringBuilder solText = new StringBuilder();
        solText.AppendLine("=== Solution région Verte — sens vus du dessus (horaire = menu Rotate) ===");
        solText.AppendLine($"Les plantes tournent seules de 45° toutes les {plantInterval:0.#} s sans lumière (Plante_1 horaire, Plante_2 anti-horaire) et s'arrêtent dès qu'elles sont éclairées.");
        solText.AppendLine($"1. Emitter_Vert : {Moves(-sE, StepOf(emitter))} → vise Plante_1.");
        solText.AppendLine("2. Si Plante_1 s'arrête dans la mauvaise direction : tourne Emitter_Vert d'un cran pour la relâcher, puis reviens quand elle pointe vers Graine_1 → le pont-liane pousse.");
        solText.AppendLine("3. Même manœuvre : rallume Plante_1 quand elle pointe vers le pont (Relay_V).");
        solText.AppendLine($"4. Relay_V : {Moves(-sR, StepOf(relayV))} pour viser Plante_2. Garde le dernier cran pour le moment où Plante_2 pointe vers Receiver_Vert.");
        solText.AppendLine($"5. Emitter_Rouge_V : {Moves(-sRd, StepOf(emitterR))} → vise Receiver_Vert (rouge + vert = jaune).");
        solText.AppendLine("→ Receiver_Vert s'allume en jaune, puis la région Verte.");
        Debug.Log(solText.ToString());

        if (errors == 0) Debug.Log($"Vérification région Verte : OK, la solution fonctionne avec le BeamManager.\n{report}");
        else Debug.LogError($"Vérification région Verte : {errors} problème(s).\n{report}");
    }

    // ---------- Relief ----------

    // Si « ok » échoue, déplace « mover » autour de « pivot » (angles × distances) jusqu'à ce que ça passe.
    private void FixLink(Crystal mover, Crystal pivot, float[] angles, float[] dists, GameObject avoid,
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
        float avoidR = avoid != null ? Footprint(avoid) + 1.5f : 0f;

        foreach (float f in dists)
            foreach (float a in angles)
            {
                if (f == 1f && a == 0f) continue;
                Vector3 p = center + Quaternion.AngleAxis(a, axis) * (pos0 - center);
                if (f != 1f) p = ArcPointU(piv, p, f);
                if (avoid != null && HDist(p, avoid.transform.position) < avoidR) continue;

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
        Debug.LogWarning($"Relief : aucune position trouvée pour {label}. Déplace l'ancre et reconstruis.");
    }

    private GameObject MakeRidge(Vector3 a, Vector3 b)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go, "Arête");
        go.name = "Arete_Rocheuse";
        go.transform.SetParent(root, false);
        Vector3 mid = ArcPoint(a, b, 0.5f);
        Vector3 up = Radial(mid);
        Vector3 fwd = Vector3.ProjectOnPlane(b - a, up).normalized;
        go.transform.SetPositionAndRotation(mid, Quaternion.LookRotation(fwd, up));
        go.transform.localScale = new Vector3(ridgeLength, ridgeHeight, 1.6f);
        if (rockMaterial != null) go.GetComponent<MeshRenderer>().sharedMaterial = rockMaterial;
        Ground(go.transform);
        SetLayer(go, LayerMask.NameToLayer("Planete"));   // compte comme du sol : coupe le rayon par la pente
        Physics.SyncTransforms();
        decor.Add(go);
        return go;
    }

    // Tablier qui suit l'enveloppe du sol avec une pente max de 25°, sur le trajet exact du rayon.
    private GameObject MakeBridge(Crystal from, Crystal to)
    {
        Physics.SyncTransforms();
        Vector3 p0 = from.BeamStart();
        Vector3 r0 = Radial(p0);
        Vector3 dir0 = Vector3.ProjectOnPlane(Heading(from, 0), r0).normalized;
        Vector3 axis = Vector3.Cross(r0, dir0).normalized;
        float R0 = (p0 - center).magnitude;
        float total = ArcLength(p0, to.transform.position) - 0.6f;
        const float ds = 0.2f;
        int n = Mathf.Max(4, Mathf.CeilToInt(total / ds));

        Vector3[] dirs = new Vector3[n + 1];
        float[] g = new float[n + 1];
        float last = R0 - 1f;
        for (int i = 0; i <= n; i++)
        {
            dirs[i] = Quaternion.AngleAxis(i * ds / R0 * Mathf.Rad2Deg, axis) * r0;
            float gr = GroundRadius(dirs[i], groundMask, R0);
            g[i] = gr < 0f ? last : gr;
            last = g[i];
        }

        float k = Mathf.Tan(25f * Mathf.Deg2Rad) * ds;
        float[] d = new float[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float m = g[i];
            for (int j = 0; j <= n; j++) m = Mathf.Max(m, g[j] - k * Mathf.Abs(i - j));
            d[i] = m;
        }

        int a = -1, b = -1;
        for (int i = 0; i <= n; i++)
            if (d[i] - g[i] > 0.05f) { if (a < 0) a = i; b = i; }
        if (a < 0) return null;
        a = Mathf.Max(0, a - 4);
        b = Mathf.Min(n, b + 4);

        GameObject bridge = new GameObject("Pont_Liane");
        Undo.RegisterCreatedObjectUndo(bridge, "Pont");
        bridge.transform.SetParent(root, false);
        Vector3 start = center + dirs[a] * d[a];
        Vector3 next = center + dirs[a + 1] * d[a + 1];
        bridge.transform.SetPositionAndRotation(start, Quaternion.LookRotation(Vector3.ProjectOnPlane(next - start, dirs[a]).normalized, dirs[a]));

        int planete = LayerMask.NameToLayer("Planete");
        for (int i = a; i < b; i += 2)
        {
            int j = Mathf.Min(i + 2, b);
            Vector3 pa = center + dirs[i] * d[i], pb = center + dirs[j] * d[j];
            Vector3 f = pb - pa;
            Vector3 mid = (pa + pb) * 0.5f;
            Vector3 up = Vector3.ProjectOnPlane(Radial(mid), f).normalized;

            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(piece, "Liane");
            piece.name = "Liane";
            piece.transform.SetParent(bridge.transform, true);
            piece.transform.rotation = Quaternion.LookRotation(f.normalized, up);
            piece.transform.position = mid - up * 0.15f;   // dessus du tablier sur le profil
            piece.transform.localScale = new Vector3(2.2f, 0.3f, f.magnitude * 1.2f);
            piece.layer = planete;                          // sol pour les rayons et le joueur
            if (vineMaterial != null) piece.GetComponent<MeshRenderer>().sharedMaterial = vineMaterial;
        }

        Debug.Log($"Pont : {(b - a) * ds:0.0} m de liane, hauteur max {Max(d, g, a, b):0.0} m au-dessus du sol.");
        bridge.SetActive(false);
        Physics.SyncTransforms();
        return bridge;
    }

    private static float Max(float[] d, float[] g, int a, int b)
    {
        float m = 0f;
        for (int i = a; i <= b; i++) m = Mathf.Max(m, d[i] - g[i]);
        return m;
    }

    // ---------- Graine ----------

    private Crystal MakeSeed(Crystal plant, out int gStep)
    {
        int full = Turn(plant);
        int[] stepTry = { -2, 2, -3, 3, -1, 1, 4 };
        float[] distTry = { 7f, 6f, 8.5f, 5f };
        Vector3 start = plant.BeamStart();
        Physics.SyncTransforms();

        Vector3 bestPos = GridTarget(plant, 0, -2, 7f * spacing);
        int bestStep = -2;
        float bestScore = -1f;
        foreach (int st in stepTry)
            foreach (float d0 in distTry)
            {
                if (Mod(st, full) == 0) continue;
                float d = d0 * spacing;
                if (!PathFree(plant, 0, st, d)) continue;
                Vector3 p = GridTarget(plant, 0, st, d);
                float score = Mathf.Min(BeamClearance(p), CrystalClearance(p), PathClearance(start, p, plant) + 1f);
                if (score > bestScore) { bestScore = score; bestPos = p; bestStep = st; }
                if (score >= 3f) goto Found;
            }
        if (bestScore < 0f) Debug.LogWarning("Graine_1 : aucun trajet libre depuis Plante_1 (relief), position par défaut.");
        else Debug.LogWarning($"Graine_1 : pas de place à 3 m des rayons, meilleure position à {bestScore:0.0} m.");
    Found:
        gStep = bestStep;
        Crystal g = MakeCrystal(receiverPrefab, "Graine_1", bestPos, CrystalType.Receiver, Colors.None, Colors.Green, false, false);
        placed.Add(g);
        beams.Add(new Beam { a = start, b = g.transform.position, label = "Plante_1 → Graine_1" });
        return g;
    }

    // ---------- Raccourcis et jungle ----------

    private List<Shortcut> FindShortcuts(GameObject bridge, Crystal emitter, Crystal plant1, Crystal relayV,
                                         Crystal graine, Crystal plant2, Crystal receiver, int gStep, Crystal emitterR)
    {
        List<Shortcut> list = new List<Shortcut>();
        void Test(Crystal src, int[] skip, Crystal[] dsts, bool on)
        {
            if (bridge != null) bridge.SetActive(on);
            Physics.SyncTransforms();
            int full = Turn(src);
            for (int k = 0; k < full; k++)
            {
                if (System.Array.IndexOf(skip, k) >= 0) continue;
                foreach (Crystal d in dsts)
                    if (TraceAt(src, k, d) == 0) list.Add(new Shortcut { src = src, dst = d, steps = k, bridgeOn = on });
            }
        }
        Test(emitter, new[] { 0 }, new[] { graine, relayV, plant2, receiver }, false);
        Test(emitter, new[] { 0 }, new[] { relayV, plant2, receiver }, true);
        Test(plant1, new[] { 0, Mod(gStep, Turn(plant1)) }, new[] { receiver }, true);
        Test(relayV, new[] { 0 }, new[] { receiver }, true);
        Test(emitterR, new[] { 0 }, new[] { graine, plant1, relayV, plant2 }, false);
        Test(emitterR, new[] { 0 }, new[] { plant1, relayV, plant2 }, true);
        if (bridge != null) bridge.SetActive(false);
        Physics.SyncTransforms();
        return list;
    }

    private int BlockShortcuts(GameObject bridge, Crystal emitter, Crystal plant1, Crystal relayV,
                               Crystal graine, Crystal plant2, Crystal receiver, int gStep, Crystal emitterR)
    {
        int count = 0;
        HashSet<string> failed = new HashSet<string>();
        for (int pass = 0; pass < 10; pass++)
        {
            List<Shortcut> list = FindShortcuts(bridge, emitter, plant1, relayV, graine, plant2, receiver, gStep, emitterR);
            list.RemoveAll(s => failed.Contains($"{s.src.name}{s.steps}{s.dst.name}"));
            if (list.Count == 0) break;
            Shortcut sc = list[0];
            if (PlaceTrunkOn(sc, count)) count++;
            else
            {
                failed.Add($"{sc.src.name}{sc.steps}{sc.dst.name}");
                Debug.LogWarning($"Raccourci {sc.src.name} (cran {sc.steps}) → {sc.dst.name} : pas de place pour un tronc.");
            }
        }
        return count;
    }

    private bool PlaceTrunkOn(Shortcut s, int index)
    {
        Quaternion keep = s.src.transform.rotation;
        s.src.transform.rotation = keep * Quaternion.Euler(0f, s.steps * StepOf(s.src), 0f);
        Vector3 p0 = s.src.BeamStart();
        Vector3 h = Heading(s.src, 0);
        s.src.transform.rotation = keep;
        float L = ArcLength(p0, s.dst.transform.position);

        float bestF = -1f, bestDelta = float.MaxValue;
        for (float f = 0.25f; f <= 0.801f; f += 0.05f)
        {
            Vector3 p = Trace(p0, h, L * f);
            if (Mathf.Min(BeamClearance(p), CrystalClearance(p)) < 1.6f) continue;
            float delta = Mathf.Abs(f - 0.5f);
            if (delta < bestDelta) { bestDelta = delta; bestF = f; }
        }
        if (bestF < 0f) return false;

        MakeTrunk(Trace(p0, h, L * bestF), $"Tronc_Raccourci_{index + 1}");
        Physics.SyncTransforms();
        return true;
    }

    private void MakeJungle()
    {
        Random.State keep = Random.state;
        Random.InitState(seed);
        int made = 0;
        for (int i = 0; i < 400 && made < jungleCount; i++)
        {
            bool mush = made % 3 == 2;
            Vector3 p = At(Random.Range(-30f, 10f), Random.Range(-16f, 20f));
            if (BeamClearance(p) < (mush ? 2.8f : 1.8f) || CrystalClearance(p) < 3f || DecorClearance(p) < 2.5f) continue;
            if (mush) MakeMushroom(p, $"Champignon_{made + 1}");
            else MakeTrunk(p, $"Tronc_{made + 1}");
            made++;
        }
        Random.state = keep;
        Physics.SyncTransforms();
    }

    private GameObject SpawnDecor(GameObject prefab)
    {
        GameObject go = PrefabUtility.IsPartOfPrefabAsset(prefab)
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
            : Instantiate(prefab);
        go.transform.localScale = prefab.transform.localScale * Random.Range(decorScale.x, decorScale.y);
        return go;
    }

    private GameObject MakeTrunk(Vector3 pos, string objName)
    {
        GameObject t;
        if (treePrefab != null) t = SpawnDecor(treePrefab);
        else
        {
            t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            t.transform.localScale = new Vector3(1f, 2.5f, 1f);
            if (trunkMaterial != null) t.GetComponent<MeshRenderer>().sharedMaterial = trunkMaterial;
        }
        PlaceObstacle(t, objName, pos);
        Ground(t.transform);
        decor.Add(t);
        return t;
    }

    private GameObject MakeMushroom(Vector3 pos, string objName)
    {
        if (mushroomPrefab != null)
        {
            GameObject mp = SpawnDecor(mushroomPrefab);
            PlaceObstacle(mp, objName, pos);
            Ground(mp.transform);
            decor.Add(mp);
            return mp;
        }

        GameObject m = new GameObject(objName);
        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "Pied";
        stem.transform.SetParent(m.transform, false);
        stem.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        stem.transform.localScale = new Vector3(0.6f, 1.2f, 0.6f);
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cap.name = "Chapeau";
        cap.transform.SetParent(m.transform, false);
        cap.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        cap.transform.localScale = new Vector3(2.6f, 0.9f, 2.6f);
        if (mushroomMaterial != null)
        {
            stem.GetComponent<MeshRenderer>().sharedMaterial = mushroomMaterial;
            cap.GetComponent<MeshRenderer>().sharedMaterial = mushroomMaterial;
        }
        PlaceObstacle(m, objName, pos);
        Ground(m.transform);
        decor.Add(m);
        return m;
    }

    private float DecorClearance(Vector3 p)
    {
        float min = float.MaxValue;
        foreach (GameObject d in decor) min = Mathf.Min(min, HDist(d.transform.position, p) - (d.name == "Arete_Rocheuse" ? ridgeLength * 0.5f : 0f));
        return min;
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

    // 0 = atteint dst, 1 = bloqué par un objet, 2 = coupé par une pente, 3 = rien touché sur « limit » mètres.
    private int TraceCore(Crystal src, int output, Crystal dst, float limit, out string why)
    {
        int beamMask = Field(mgr, "beamMask", ~0);
        int gMask = Field(mgr, "groundMask", groundMask);
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

    private bool PathFree(Crystal src, int output, int steps, float dist)
    {
        Quaternion keep = src.transform.rotation;
        src.transform.rotation = keep * Quaternion.Euler(0f, steps * StepOf(src), 0f);
        bool ok = TraceCore(src, output, null, dist, out _) == 3;
        src.transform.rotation = keep;
        return ok;
    }

    private static string ForcedOff(Crystal[] all)
    {
        FieldInfo f = null;
        foreach (FieldInfo fi in typeof(Crystal).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            if (fi.FieldType == typeof(bool) && fi.Name.ToLowerInvariant().Contains("forced")) { f = fi; break; }
        if (f == null) return "";
        List<string> names = new List<string>();
        foreach (Crystal c in all) if ((bool)f.GetValue(c)) names.Add(c.name);
        return string.Join(", ", names);
    }

    // ---------- Placement ----------

    private Vector3 At(float x, float z) => root.TransformPoint(new Vector3((mirrorX ? -x : x) * spacing, 0f, z * spacing));
    private Vector3 Radial(Vector3 p) => (p - center).normalized;
    private static int Mod(int a, int m) => ((a % m) + m) % m;
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

    private Crystal MakePlant(GameObject prefab, string objName, Vector3 worldPos, int direction)
    {
        Crystal c = MakeCrystal(prefab, objName, worldPos, CrystalType.Relay, Colors.None, Colors.None, false, true);
        SerializedObject so = new SerializedObject(c);
        Prop(so, "stepAngle").floatValue = 45f;
        Prop(so, "rotationSpeed").floatValue = plantRotationSpeed;
        so.ApplyModifiedPropertiesWithoutUndo();

        Phototropisme ph = c.GetComponent<Phototropisme>();
        if (ph == null) ph = Undo.AddComponent<Phototropisme>(c.gameObject);
        SerializedObject sp = new SerializedObject(ph);
        Prop(sp, "interval").floatValue = plantInterval;
        Prop(sp, "direction").intValue = direction;
        sp.ApplyModifiedPropertiesWithoutUndo();
        return c;
    }

    private float HDist(Vector3 a, Vector3 b)
    {
        Vector3 d = a - b;
        Vector3 r = Radial(b);
        return (d - r * Vector3.Dot(d, r)).magnitude;
    }

    private float BeamClearance(Vector3 p)
    {
        float min = float.MaxValue;
        foreach (Beam bm in beams) min = Mathf.Min(min, DistToBeam(p, bm.a, bm.b));
        return min;
    }

    private float CrystalClearance(Vector3 p)
    {
        float min = float.MaxValue;
        foreach (Crystal c in placed) min = Mathf.Min(min, HDist(c.transform.position, p));
        return min;
    }

    private float PathClearance(Vector3 a, Vector3 b, Crystal source)
    {
        float min = float.MaxValue;
        foreach (Crystal c in placed)
            if (c != source) min = Mathf.Min(min, DistToBeam(c.transform.position, a, b));
        return min;
    }

    private float DistToBeam(Vector3 o, Vector3 a, Vector3 b)
    {
        float min = float.MaxValue;
        for (int i = 1; i < 40; i++) min = Mathf.Min(min, HDist(o, ArcPoint(a, b, i / 40f)));
        return min;
    }

    private static float StepOf(Crystal c) => new SerializedObject(c).FindProperty("stepAngle").floatValue;

    // ---------- Rayons ----------

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

    private float GridPhase(Crystal source, int output, Vector3 pos)
    {
        Vector3 p = source.BeamStart();
        Vector3 r = Radial(p);
        Vector3 h = Vector3.ProjectOnPlane(Heading(source, output), r);
        Vector3 d = Vector3.ProjectOnPlane(pos - p, r);
        float s = StepOf(source);
        return Mathf.Repeat(Vector3.SignedAngle(h, d, r) + s * 0.5f, s) - s * 0.5f;
    }

    private void MoveAround(Crystal target, Vector3 axis, float angle)
    {
        if (Mathf.Abs(angle) < 0.001f) return;
        Undo.RecordObject(target.transform, "Anti-retour");
        target.transform.position = center + Quaternion.AngleAxis(angle, axis) * (target.transform.position - center);
        target.AlignToPlanet(false);
    }

    // Déplace la cible visée par source pour que la grille de source passe à mi-cran de « avoid ».
    private void MoveAimTarget(Crystal source, int output, Crystal aimTarget, Crystal avoid)
    {
        float s = StepOf(source);
        float phase = GridPhase(source, output, avoid.transform.position);
        float wanted = phase >= 0f ? s * 0.5f : -s * 0.5f;
        MoveAround(aimTarget, Radial(source.BeamStart()), phase - wanted);
        AimExact(source, aimTarget.transform.position, output);
    }

    private Vector3 GridTarget(Crystal c, int index, int steps, float dist)
    {
        Quaternion keep = c.transform.rotation;
        c.transform.rotation = keep * Quaternion.Euler(0f, steps * StepOf(c), 0f);
        Vector3 h = Heading(c, index);
        Vector3 p = c.BeamStart();
        c.transform.rotation = keep;
        return Trace(p, h, dist);
    }

    private Vector3 Trace(Vector3 p, Vector3 heading, float dist)
    {
        Vector3 r = Radial(p);
        float R = (p - center).magnitude;
        Vector3 t = Vector3.ProjectOnPlane(heading, r).normalized;
        float th = dist / R;
        return center + (r * Mathf.Cos(th) + t * Mathf.Sin(th)) * R;
    }

    private Vector3 ArcPoint(Vector3 a, Vector3 b, float f)
    {
        Vector3 ra = a - center, rb = b - center;
        return center + Vector3.Slerp(ra.normalized, rb.normalized, f) * Mathf.Lerp(ra.magnitude, rb.magnitude, f);
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

    private void AddBeam(Crystal s, Crystal d) =>
        beams.Add(new Beam { a = s.BeamStart(), b = d.transform.position, label = $"{s.name} → {d.name}" });

    private static float Footprint(GameObject go)
    {
        float f = 0f;
        foreach (Collider c in go.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled) continue;
            Vector3 e = c.bounds.extents;
            f = Mathf.Max(f, e.x, e.y, e.z);
        }
        return f;
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

    // ---------- Décor ----------

    private static void SetLayer(GameObject go, int layer)
    {
        if (layer < 0) return;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    private void PlaceObstacle(GameObject go, string objName, Vector3 worldPos)
    {
        Undo.RegisterCreatedObjectUndo(go, objName);
        go.name = objName;
        go.transform.SetParent(root, false);
        go.transform.position = worldPos;

        int layer = LayerMask.NameToLayer(obstacleLayer);
        if (layer < 0) Debug.LogWarning($"{name} : layer « {obstacleLayer} » introuvable.");
        else if (layer == LayerMask.NameToLayer("Planete")) Debug.LogWarning($"{name} : l'Obstacle Layer ne doit pas être Planete.");
        else SetLayer(go, layer);

        Vector3 up = Radial(worldPos);
        Vector3 fwd = Vector3.ProjectOnPlane(root.forward, up);
        go.transform.rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), up) * Quaternion.LookRotation(fwd.normalized, up);
    }

    // Pose la base de l'objet sur le point de sol le plus bas sous son emprise, puis l'enfonce de obstacleSink.
    private void Ground(Transform t)
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
            Debug.LogWarning($"{t.name} : sol introuvable sous l'obstacle.");
            return;
        }

        Undo.RecordObject(t, "Ground");
        t.position = center + up * (ground - obstacleSink - minAlong);
    }

    private static ReceiverWatcher Watch(Crystal receiver)
    {
        ReceiverWatcher w = Undo.AddComponent<ReceiverWatcher>(receiver.gameObject);
        SerializedObject so = new SerializedObject(w);
        Prop(so, "receiver").objectReferenceValue = receiver;
        Prop(so, "crystalsToUnlock").arraySize = 0;
        Prop(so, "objectsToHide").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        return w;
    }
#endif
}