using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif

// Construit en un clic (hors Play) le puzzle de la région Rouge sous cette ancre, puis le VÉRIFIE
// avec le vrai BeamManager. Receiver_Rouge (magenta = rouge + bleu) doit être CHARGÉ ; le rouge passe
// par deux relais qui surchauffent, il faut alterner. Rochers sur les raccourcis, lave décorative.
// Coordonnées locales : X sur le côté, Z vers le pôle (le Phare). Les distances suivent la surface.
public class RedRegionBuilder : MonoBehaviour
{
    [Header("Prefabs (composant Crystal à la racine)")]
    [SerializeField] private GameObject emitterPrefab;
    [SerializeField] private GameObject relayPrefab;
    [SerializeField] private GameObject receiverPrefab;

    [Header("Surchauffe et charge")]
    [Tooltip("Secondes à relayer avant qu'un relais surchauffe.")]
    [SerializeField] private float heatTime = 4f;
    [Tooltip("Vitesse de refroidissement (1 = aussi vite qu'il chauffe).")]
    [SerializeField] private float coolRate = 1f;
    [Tooltip("Secondes cumulées de magenta pour charger Receiver_Rouge.")]
    [SerializeField] private float chargeTime = 10f;
    [Tooltip("Charge perdue par seconde dans le noir. Garder ≥ Cool Rate, sinon un seul chemin suffit.")]
    [SerializeField] private float decayRate = 1f;
    [Tooltip("Degrés par seconde d'Emitter_Rouge quand le joueur le tourne (bascule rapide).")]
    [SerializeField] private float redRotationSpeed = 60f;
    [Tooltip("Crans entre la visée du récepteur et chaque relais (2 = 30° de chaque côté).")]
    [SerializeField] private int pathSteps = 2;

    [Header("Décor")]
    [SerializeField] private Material rockMaterial;
    [SerializeField] private Material lavaMaterial;
    [Tooltip("Vide = sphère simple. Doit garder un collider d'au moins 1,5 m de large.")]
    [SerializeField] private GameObject rockPrefab;
    [Tooltip("Échelle aléatoire min / max des rochers.")]
    [SerializeField] private Vector2 decorScale = new Vector2(0.85f, 1.3f);
    [Tooltip("Layer des rochers : coché dans le Beam Mask, différent de Planete.")]
    [SerializeField] private string obstacleLayer = "Default";
    [SerializeField] private float obstacleSink = 0.4f;
    [Tooltip("Rayon (m, sur la surface) du disque de décor autour de l'ancre.")]
    [SerializeField] private float decorRadius = 22f;
    [SerializeField] private int rockCount = 14;
    [SerializeField] private int lavaCount = 7;
    [SerializeField] private int seed = 11;

    [Header("Région")]
    [Tooltip("Vide = cherché automatiquement dans la scène.")]
    [SerializeField] private RegionLight regionLight;
    [SerializeField] private string regionProperty = "_Lit_Rouge";
    [Tooltip("Coché : le puzzle est inversé gauche / droite.")]
    [SerializeField] private bool mirrorX;
    [Tooltip("Taille du puzzle (mètres par unité de grille).")]
    [SerializeField] private float spacing = 1.6f;

#if UNITY_EDITOR
    private const string RootName = "Puzzle_Rouge";
    private Transform root;
    private Vector3 center;
    private int groundMask;
    private BeamManager mgr;

    private struct Beam { public Vector3 a, b; public string label; }
    private struct Shortcut { public Crystal src, dst; public int steps; }
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

        foreach (GameObject p in new[] { emitterPrefab, relayPrefab, receiverPrefab })
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

        // Le layer des rochers doit arrêter les rayons.
        int obsLayer = LayerMask.NameToLayer(obstacleLayer);
        if (obsLayer < 0) { Debug.LogError($"{name} : le layer « {obstacleLayer} » n'existe pas (champ Obstacle Layer)."); return; }
        int beamMaskValue = Field(manager, "beamMask", ~0);
        if ((beamMaskValue & (1 << obsLayer)) == 0)
        {
            Debug.LogError($"{name} : le layer « {obstacleLayer} » n'est pas coché dans le Beam Mask du BeamManager : les rochers ne bloqueraient pas les rayons. " +
                           "Mets dans Obstacle Layer le même layer que pour la région Verte, ou coche-le dans Beam Mask.");
            return;
        }

        mgr = manager;
        if (regionLight == null) regionLight = FindFirstObjectByType<RegionLight>();
        if (pathSteps < 1) pathSteps = 1;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Construire la région Rouge");
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
        Crystal receiver = MakeCrystal(receiverPrefab, "Receiver_Rouge", At(0f, 9f),  CrystalType.Receiver, Colors.None, Colors.Purple, false, true);
        Crystal emitterR = MakeCrystal(emitterPrefab,  "Emitter_Rouge",  At(0f, -8f), CrystalType.Emitter,  Colors.Red,  Colors.None,   true,  true);
        Crystal emitterB = MakeCrystal(emitterPrefab,  "Emitter_Bleu_R", At(11f, 7f), CrystalType.Emitter,  Colors.Blue, Colors.None,   true,  true);
        Crystal s1 = MakeCrystal(relayPrefab, "Surchauffe_1", At(-4f, 0f), CrystalType.Relay, Colors.None, Colors.None, true, true);
        Crystal s2 = MakeCrystal(relayPrefab, "Surchauffe_2", At(4f, 0f),  CrystalType.Relay, Colors.None, Colors.None, true, true);
        placed.AddRange(new[] { receiver, emitterR, emitterB, s1, s2 });

        SerializedObject so = new SerializedObject(emitterR);
        Prop(so, "rotationSpeed").floatValue = redRotationSpeed;
        so.ApplyModifiedPropertiesWithoutUndo();

        // --- 2. Emitter_Rouge + relais : l'émetteur tourne autour du récepteur jusqu'à ce que les deux relais trouvent leur place ---
        float[] eAngles = { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 90f, -90f, 120f, -120f };
        float[] eDists = { 1f, 0.85f, 1.15f, 0.7f, 1.3f };
        FixLink(emitterR, receiver, eAngles, eDists,
                () => AimExact(emitterR, receiver.transform.position, 0),
                () => PlaceRelay(emitterR, -pathSteps, s1, receiver, false) && PlaceRelay(emitterR, pathSteps, s2, receiver, false),
                "Emitter_Rouge");
        PlaceRelay(emitterR, -pathSteps, s1, receiver, true);
        PlaceRelay(emitterR, pathSteps, s2, receiver, true);
        AddHeat(s1);
        AddHeat(s2);

        // --- 3. Bleu direct ---
        float[] dists = { 1f, 0.85f, 1.15f, 0.7f, 1.3f };
        FixLink(emitterB, receiver, new[] { 0f, 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f, 90f, -90f }, dists,
                () => AimExact(emitterB, receiver.transform.position, 0), () => Reaches(emitterB, 0, receiver, out _), "Emitter_Bleu_R");

        AddBeam(emitterR, s1); AddBeam(emitterR, s2);
        AddBeam(s1, receiver); AddBeam(s2, receiver);
        AddBeam(emitterB, receiver);

        Crystal[] all = { receiver, emitterR, emitterB, s1, s2 };
        Dictionary<Crystal, Quaternion> sol = new Dictionary<Crystal, Quaternion>();
        foreach (Crystal c in new[] { emitterR, emitterB, s1, s2 }) sol[c] = c.transform.rotation;

        // --- 4. Rochers sur les raccourcis, puis décor ---
        int blockers = BlockShortcuts(emitterR, receiver);
        if (blockers > 0) Debug.Log($"Région Rouge : {blockers} rocher(s) posé(s) pour fermer le tir rouge direct.");
        MakeDecor();

        // --- 5. Charge et allumage ---
        Charge charge = Undo.AddComponent<Charge>(receiver.gameObject);
        SerializedObject sc = new SerializedObject(charge);
        Prop(sc, "chargeTime").floatValue = chargeTime;
        Prop(sc, "decayRate").floatValue = decayRate;
        SerializedProperty arr = Prop(sc, "stabilizeOnCharged");
        arr.arraySize = 2;
        arr.GetArrayElementAtIndex(0).objectReferenceValue = s1.GetComponent<Surchauffe>();
        arr.GetArrayElementAtIndex(1).objectReferenceValue = s2.GetComponent<Surchauffe>();
        Prop(sc, "chargeLight").objectReferenceValue = MakeLight(receiver, "Lumiere_Charge", new Color(1f, 0.2f, 1f), 6f);
        sc.ApplyModifiedPropertiesWithoutUndo();

        if (regionLight != null)
        {
            Undo.RecordObject(charge, "Region light");
            UnityEventTools.AddStringPersistentListener(charge.onCharged, regionLight.LightRegion, regionProperty);
            EditorUtility.SetDirty(charge);
        }
        else Debug.LogWarning($"{name} : aucun RegionLight trouvé, branche On Charged de Receiver_Rouge à la main.");

        // --- 6. Orientation de départ ---
        const int sR = 7, sS1 = 5, sS2 = -4, sB = -5;
        RotateSteps(emitterR, sR);
        RotateSteps(emitterB, sB);
        RotateSteps(s1, sS1);
        RotateSteps(s2, sS2);

        // --- 7. Vérification avec le vrai BeamManager ---
        int errors = 0;
        StringBuilder report = new StringBuilder();
        void Pose(Crystal c, int n) => c.transform.rotation = sol[c] * Quaternion.Euler(0f, n * StepOf(c), 0f);
        void Run() { Physics.SyncTransforms(); manager.Recompute(); }
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
        Pose(emitterR, sR); Pose(emitterB, sB); Pose(s1, sS1); Pose(s2, sS2);
        Run();
        Expect(!receiver.IsSatisfied, "Départ : Receiver_Rouge est déjà satisfait sans rien faire.");

        // Les deux chemins
        Pose(emitterB, 0); Pose(s1, 0); Pose(s2, 0);
        for (int p = 0; p < 2; p++)
        {
            Crystal s = p == 0 ? s1 : s2;
            int step = p == 0 ? -pathSteps : pathSteps;
            Pose(emitterR, step);
            Run();
            ExpectBeam(s.OutputColor == Colors.Red, $"Chemin {p + 1} : {s.name} ne relaie pas le rouge (reçoit {s.ReceivedColor}).", emitterR, s);
            ExpectBeam(receiver.IsSatisfied, $"Chemin {p + 1} : Receiver_Rouge reçoit {receiver.ReceivedColor} au lieu de Purple.", s, receiver);
            string f = ForcedOff(all);
            Expect(f.Length == 0, $"Chemin {p + 1} : cristaux éteints de force par le BeamManager (boucle instable) : {f}.");
        }
        ExpectBeam(Reaches(emitterB, 0, receiver, out _), "Le rayon bleu n'atteint pas Receiver_Rouge.", emitterB, receiver);

        // Rouge seul, et rouge sans relais
        Pose(emitterB, sB); Pose(emitterR, -pathSteps);
        Run();
        Expect(!receiver.IsSatisfied, "Rouge seul : Receiver_Rouge s'allume sans le bleu.");
        Pose(emitterB, 0);
        for (int k = -pathSteps + 1; k < pathSteps; k++)
        {
            Pose(emitterR, k);
            Run();
            if (receiver.IsSatisfied)
            {
                Expect(false, $"Emitter_Rouge au cran {k} : Receiver_Rouge s'allume sans passer par un relais.");
                Reaches(emitterR, 0, receiver, out string why);
                report.AppendLine($"   ↳ rayon Emitter_Rouge → Receiver_Rouge : {why}");
            }
        }

        // Boucles : tour complet de chaque relais pendant qu'il est alimenté
        for (int p = 0; p < 2; p++)
        {
            Crystal s = p == 0 ? s1 : s2;
            Pose(emitterR, p == 0 ? -pathSteps : pathSteps);
            for (int k = 0; k < Turn(s); k++)
            {
                Pose(s, k);
                Run();
                string f = ForcedOff(all);
                if (f.Length > 0) report.AppendLine($"⚠ {s.name} au cran {k} crée une boucle ({f}) : le BeamManager l'éteint, sans bloquer le puzzle.");
            }
            Pose(s, 0);
        }

        // Raccourcis restants
        Pose(emitterR, 0);
        foreach (Shortcut s in FindShortcuts(emitterR, receiver))
            report.AppendLine($"⚠ Raccourci : Emitter_Rouge au cran {s.steps} atteint Receiver_Rouge directement (sans surchauffe).");

        // Réglages de temps
        if (chargeTime <= heatTime) Expect(false, $"Charge Time ({chargeTime:0.#}) ≤ Heat Time ({heatTime:0.#}) : un seul chemin suffit.");
        if (decayRate < coolRate) report.AppendLine("⚠ Decay Rate < Cool Rate : en insistant sur un seul relais, le récepteur finit par se charger.");
        float swap = 2 * pathSteps * StepOf(emitterR) / Mathf.Max(1f, redRotationSpeed);
        report.AppendLine($"ℹ Bascule entre les relais : {2 * pathSteps} crans, environ {swap:0.0} s sans rayon.");
        if (swap >= heatTime * 0.5f) report.AppendLine("⚠ Bascule lente par rapport à Heat Time : augmente Red Rotation Speed.");

        // Retour à l'état de départ
        Pose(emitterR, sR); Pose(emitterB, sB); Pose(s1, sS1); Pose(s2, sS2);
        Run();

        Undo.CollapseUndoOperations(group);

        // --- 8. Solution + rapport ---
        float stepR = StepOf(emitterR);
        StringBuilder solText = new StringBuilder();
        solText.AppendLine("=== Solution région Rouge — sens vus du dessus (horaire = menu Rotate) ===");
        solText.AppendLine($"Receiver_Rouge doit rester magenta {chargeTime:0.#} s au total. Chaque relais surchauffe après {heatTime:0.#} s et s'éteint jusqu'à refroidir.");
        solText.AppendLine($"1. Emitter_Bleu_R : {Moves(-sB, StepOf(emitterB))} → vise Receiver_Rouge.");
        solText.AppendLine($"2. Surchauffe_1 : {Moves(-sS1, StepOf(s1))} ; Surchauffe_2 : {Moves(-sS2, StepOf(s2))} → visent Receiver_Rouge.");
        solText.AppendLine($"3. Emitter_Rouge : {Moves(-pathSteps - sR, stepR)} → vise Surchauffe_1 : le récepteur devient magenta et se charge.");
        solText.AppendLine($"4. Avant la surchauffe, bascule Emitter_Rouge vers Surchauffe_2 ({Moves(2 * pathSteps, stepR)}), puis reviens ({Moves(-2 * pathSteps, stepR)}), et ainsi de suite.");
        solText.AppendLine("→ Une fois chargé, les relais se stabilisent et la région Rouge s'allume.");
        Debug.Log(solText.ToString());

        if (errors == 0) Debug.Log($"Vérification région Rouge : OK, la solution fonctionne avec le BeamManager.\n{report}");
        else Debug.LogError($"Vérification région Rouge : {errors} problème(s).\n{report}");
    }

    // ---------- Relais de surchauffe ----------

    // Place le relais dans la direction « step » de src, à la première distance où src l'atteint et où il atteint le récepteur.
    private bool PlaceRelay(Crystal src, int step, Crystal c, Crystal receiver, bool log)
    {
        float[] tries = { 8f, 7f, 9f, 6f, 10f, 5f, 11f };
        foreach (float d in tries)
        {
            c.transform.SetPositionAndRotation(GridTarget(src, 0, step, d * spacing), root.rotation);
            c.AlignToPlanet(false);
            AimExact(c, receiver.transform.position, 0);
            Physics.SyncTransforms();
            if (TraceAt(src, step, c) == 0 && Reaches(c, 0, receiver, out _))
            {
                if (log && d != tries[0]) Debug.Log($"Relief : {c.name} placé à {d * spacing:0.0} m d'Emitter_Rouge.");
                return true;
            }
        }
        c.transform.SetPositionAndRotation(GridTarget(src, 0, step, tries[0] * spacing), root.rotation);
        c.AlignToPlanet(false);
        AimExact(c, receiver.transform.position, 0);
        Physics.SyncTransforms();
        if (log)
        {
            TraceAtWhy(src, step, c, out string why);
            Debug.LogWarning($"Relief : aucune position trouvée pour {c.name} ({why}). Déplace l'ancre et reconstruis.");
        }
        return false;
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

    // ---------- Relief ----------

    // Si « ok » échoue, déplace « mover » autour de « pivot » (angles × distances) jusqu'à ce que ça passe.
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
        Debug.LogWarning($"Relief : aucune position trouvée pour {label}. Déplace l'ancre et reconstruis.");
    }

    // ---------- Raccourcis et décor ----------

    private List<Shortcut> FindShortcuts(Crystal emitterR, Crystal receiver)
    {
        List<Shortcut> list = new List<Shortcut>();
        Physics.SyncTransforms();
        for (int k = 0; k < Turn(emitterR); k++)
            if (TraceAt(emitterR, k, receiver) == 0) list.Add(new Shortcut { src = emitterR, dst = receiver, steps = k });
        return list;
    }

    private int BlockShortcuts(Crystal emitterR, Crystal receiver)
    {
        int count = 0;
        HashSet<int> failed = new HashSet<int>();
        for (int pass = 0; pass < 8; pass++)
        {
            List<Shortcut> list = FindShortcuts(emitterR, receiver);
            list.RemoveAll(s => failed.Contains(s.steps));
            if (list.Count == 0) break;
            Shortcut sc = list[0];
            if (PlaceRockOn(sc, count, out string why)) count++;
            else
            {
                failed.Add(sc.steps);
                Debug.LogWarning($"Raccourci Emitter_Rouge (cran {sc.steps}) → Receiver_Rouge : impossible à fermer par un rocher. {why}");
            }
        }
        return count;
    }

    // Pose un rocher sur le tir, vérifie qu'il coupe vraiment le rayon, le rehausse sinon.
    private bool PlaceRockOn(Shortcut s, int index, out string why)
    {
        why = "";
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
            if (Mathf.Min(BeamClearance(p), CrystalClearance(p)) < 1.8f) continue;
            float delta = Mathf.Abs(f - 0.5f);
            if (delta < bestDelta) { bestDelta = delta; bestF = f; }
        }
        if (bestF < 0f) { why = "Pas de place assez loin des autres rayons et cristaux."; return false; }

        GameObject rock = MakeRock(Trace(p0, h, L * bestF), $"Rocher_Raccourci_{index + 1}");
        for (int grow = 0; grow < 7; grow++)
        {
            Physics.SyncTransforms();
            if (TraceAt(s.src, s.steps, s.dst) != 0)
            {
                if (grow > 0) Debug.Log($"{rock.name} rehaussé {grow} fois pour couper le rayon.");
                return true;
            }
            Undo.RecordObject(rock.transform, "Rehausser");
            Vector3 sc = rock.transform.localScale;
            rock.transform.localScale = new Vector3(sc.x * 1.1f, sc.y * 1.35f, sc.z * 1.1f);   // Y local = vers l'extérieur
            Ground(rock.transform, obstacleSink);
        }

        Physics.SyncTransforms();
        TraceAtWhy(s.src, s.steps, s.dst, out why);
        why = "Le rayon passe encore malgré le rocher : " + why;
        decor.Remove(rock);
        Undo.DestroyObjectImmediate(rock);
        Physics.SyncTransforms();
        return false;
    }

    private void MakeDecor()
    {
        Random.State keep = Random.state;
        Random.InitState(seed);
        int rocks = 0, lava = 0;
        for (int i = 0; i < 1500 && (rocks < rockCount || lava < lavaCount); i++)
        {
            bool isLava = lava < lavaCount && (rocks >= rockCount || i % 3 == 0);
            Vector2 v = Random.insideUnitCircle * decorRadius;
            Vector3 p = AtM(v.x, v.y);
            if (BeamClearance(p) < (isLava ? 2.5f : 1.8f) || CrystalClearance(p) < 3f || DecorClearance(p) < (isLava ? 4f : 3f)) continue;
            if (isLava) { MakeLava(p, $"Lave_{lava + 1}"); lava++; }
            else { MakeRock(p, $"Rocher_{rocks + 1}"); rocks++; }
        }
        if (rocks < rockCount || lava < lavaCount)
            Debug.Log($"Décor : {rocks}/{rockCount} rochers et {lava}/{lavaCount} coulées placés (pas assez de place, augmente Decor Radius).");
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

    private GameObject MakeRock(Vector3 pos, string objName)
    {
        GameObject r;
        if (rockPrefab != null) r = SpawnDecor(rockPrefab);
        else
        {
            r = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            r.transform.localScale = new Vector3(2.2f, 1.6f, 1.9f) * Random.Range(decorScale.x, decorScale.y);
            if (rockMaterial != null) r.GetComponent<MeshRenderer>().sharedMaterial = rockMaterial;
        }
        PlaceObstacle(r, objName, pos);
        Ground(r.transform, obstacleSink);
        decor.Add(r);
        return r;
    }

    // Flaque de lave décorative : plusieurs lentilles aplaties + lueur. Pas de collider.
    private GameObject MakeLava(Vector3 pos, string objName)
    {
        GameObject l = new GameObject(objName);
        PlaceObstacle(l, objName, pos);
        GroundFlat(l.transform);

        int n = Random.Range(3, 6);
        for (int i = 0; i < n; i++)
        {
            GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(blob.GetComponent<Collider>());
            blob.name = $"Coulee_{i + 1}";
            blob.transform.SetParent(l.transform, false);
            Vector2 off = Random.insideUnitCircle * 1.3f;
            blob.transform.localPosition = new Vector3(off.x, 0f, off.y);
            blob.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            blob.transform.localScale = new Vector3(Random.Range(1.2f, 2.4f), Random.Range(0.18f, 0.3f), Random.Range(0.9f, 1.7f));
            MeshRenderer mr = blob.GetComponent<MeshRenderer>();
            if (lavaMaterial != null) mr.sharedMaterial = lavaMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        GameObject glow = new GameObject("Lueur");
        glow.transform.SetParent(l.transform, false);
        glow.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        Light light = glow.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.35f, 0.05f);
        light.range = 4.5f;
        light.intensity = 2.5f;

        decor.Add(l);
        return l;
    }

    private float DecorClearance(Vector3 p)
    {
        float min = float.MaxValue;
        foreach (GameObject d in decor) min = Mathf.Min(min, HDist(d.transform.position, p));
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
                if (dst != null && c == dst) { why = $"atteint à {at:0.0} m, à {height:0.0} m du sol (la géométrie est bonne)."; return 0; }
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

    private int TraceAt(Crystal src, int steps, Crystal dst) => TraceAtWhy(src, steps, dst, out _);

    private int TraceAtWhy(Crystal src, int steps, Crystal dst, out string why)
    {
        Quaternion keep = src.transform.rotation;
        src.transform.rotation = keep * Quaternion.Euler(0f, steps * StepOf(src), 0f);
        int r = TraceCore(src, 0, dst, float.MaxValue, out why);
        src.transform.rotation = keep;
        return r;
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

    // Unités de grille (× spacing).
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

    // Pose un objet plat (lave) à plat sur le terrain, en suivant la normale du sol.
    private void GroundFlat(Transform t)
    {
        Vector3 dir = Radial(t.position);
        float r = (t.position - center).magnitude;
        if (!Physics.Raycast(center + dir * (r + 50f), -dir, out RaycastHit hit, r + 100f, groundMask))
        {
            Debug.LogWarning($"{t.name} : sol introuvable sous la lave.");
            return;
        }
        Undo.RecordObject(t, "Ground");
        t.rotation = Quaternion.FromToRotation(t.up, hit.normal) * t.rotation;
        t.position = hit.point - hit.normal * 0.05f;
    }
#endif
}