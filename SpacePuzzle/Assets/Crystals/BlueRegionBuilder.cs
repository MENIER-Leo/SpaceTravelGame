using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif

// Construit en un clic (hors Play) le puzzle de la région Bleue sous cette ancre, puis le VÉRIFIE
// avec le vrai BeamManager (solution jouée étape par étape + tour complet du prisme et du mixer).
// Le relief est pris en compte : branche verte déplacée si une pente coupe ses rayons,
// dégels placés sur des trajets réellement libres, rochers posés hors de tous les rayons.
// Coordonnées locales : X sur le côté, Z vers le pôle (le Phare). Partie Sud : repère décalé vers l'équateur.
public class BlueRegionBuilder : MonoBehaviour
{
    [Header("Prefabs de cristaux (composant Crystal à la racine)")]
    [SerializeField] private GameObject emitterPrefab;
    [SerializeField] private GameObject relayPrefab;
    [SerializeField] private GameObject prismPrefab;
    [SerializeField] private GameObject mixerPrefab;
    [SerializeField] private GameObject receiverPrefab;

    [Header("Décor")]
    [Tooltip("Optionnel. Vide = sphère aplatie générée.")]
    [SerializeField] private GameObject rockPrefab;
    [SerializeField] private Material rockMaterial;
    [SerializeField] private Material iceMaterial;
    [Tooltip("Layer du rocher et du mur de glace : coché dans le Beam Mask, différent de Planete.")]
    [SerializeField] private string obstacleLayer = "Default";
    [Tooltip("Enfoncement des rochers et murs dans le sol, en mètres.")]
    [SerializeField] private float obstacleSink = 0.4f;

    [Header("Région")]
    [Tooltip("Vide = cherché automatiquement dans la scène.")]
    [SerializeField] private RegionLight regionLight;
    [SerializeField] private string regionProperty = "_Lit_Bleu";
    [Tooltip("Coché : le puzzle s'étend vers +X au lieu de -X.")]
    [SerializeField] private bool mirrorX;
    [Tooltip("Multiplie les distances.")]
    [SerializeField] private float spacing = 1.2f;
    [Tooltip("Décalage de la partie Sud vers l'équateur, en degrés autour du centre de la planète.")]
    [SerializeField] private float southAngle = 45f;
    [Tooltip("Distance minimale entre un dégel et les rayons de la solution, en mètres.")]
    [SerializeField] private float degelClearance = 2.5f;

#if UNITY_EDITOR
    private const string RootName = "Puzzle_Bleu";
    private Transform root;
    private Transform south;
    private Transform planet;
    private Vector3 center;
    private int groundMask;
    private BeamManager mgr;

    private struct Beam { public Vector3 a, b; public string label; public GameObject src, dst; }
    private readonly List<Beam> beams = new List<Beam>();
    private readonly List<Crystal> placed = new List<Crystal>();

    [ContextMenu("Construire le puzzle")]
    private void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning($"{name} : construction uniquement hors Play."); return; }

        foreach (GameObject p in new[] { emitterPrefab, relayPrefab, prismPrefab, mixerPrefab, receiverPrefab })
            if (p == null || p.GetComponent<Crystal>() == null)
            {
                Debug.LogWarning($"{name} : assigne les 5 prefabs de cristaux (avec Crystal à la racine).");
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
        Undo.SetCurrentGroupName("Construire la région Bleue");
        int group = Undo.GetCurrentGroup();

        Transform old = transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject rootGo = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootGo, "Puzzle root");
        rootGo.transform.SetParent(transform, false);
        root = rootGo.transform;
        planet = manager.PlanetTransform;
        center = planet.position;
        groundMask = LayerMask.GetMask("Planete");
        beams.Clear();
        placed.Clear();

        GameObject southGo = new GameObject("Sud");
        Undo.RegisterCreatedObjectUndo(southGo, "Sud");
        southGo.transform.SetParent(root, false);
        Quaternion tilt = Quaternion.AngleAxis(-southAngle, transform.right);
        southGo.transform.SetPositionAndRotation(center + tilt * (transform.position - center), tilt * transform.rotation);
        south = southGo.transform;

        int sg = mirrorX ? -1 : 1;

        // --- 1. Cristaux principaux ---
        Crystal emitterBlue  = MakeCrystal(root,  emitterPrefab,  "Emitter_Blue",  At(root,  6f,  -8f), CrystalType.Emitter,  Colors.Blue,  Colors.None, true,  true);
        Crystal relay        = MakeCrystal(root,  relayPrefab,    "Relay_B",       At(root, -4f,  -8f), CrystalType.Relay,    Colors.None,  Colors.None, false, true);
        Crystal prism        = MakeCrystal(root,  prismPrefab,    "Prism_B",       At(root,  0f,   0f), CrystalType.Prism,    Colors.None,  Colors.None, false, true);
        Crystal mixer        = MakeCrystal(root,  mixerPrefab,    "Mixer_Cyan",    At(root, -14f,  0f), CrystalType.Mixer,    Colors.None,  Colors.None, true,  true);
        Crystal receiverCyan = MakeCrystal(root,  receiverPrefab, "Receiver_Cyan", At(root, -20f,  6f), CrystalType.Receiver, Colors.None,  Colors.Cyan, false, true);
        Crystal emitterGreen = MakeCrystal(south, emitterPrefab,  "Emitter_Green", At(south, -4f, -6f), CrystalType.Emitter,  Colors.Green, Colors.None, true,  true);
        Crystal relayG       = MakeCrystal(south, relayPrefab,    "Relay_G",       At(south, -14f, 6f), CrystalType.Relay,    Colors.None,  Colors.None, false, true);
        SetHint(mixer, Colors.Cyan);
        SetStep(relayG, 45f);   // crans grippés
        placed.AddRange(new[] { emitterBlue, relay, prism, mixer, receiverCyan, emitterGreen, relayG });

        // --- 2. Orientations clés ---
        int prismOut = AimPrism(prism, mixer.transform.position, root.forward);
        AimExact(mixer, receiverCyan.transform.position, 0);

        // --- 3. Anti-retour : aucun cran du prisme ou du mixer ne doit renvoyer un rayon vers un fournisseur ---
        MoveAimTarget(mixer, 0, receiverCyan, prism);   // le mixer ne peut jamais viser le prisme
        MoveToHalfStep(mixer, 0, relayG);               // ni Relay_G
        AvoidBoth(prism, prismOut, mixer, relay);       // ni le prisme ni le mixer ne peuvent viser Relay_B

        // --- 4. Autres orientations de la solution ---
        AimExact(relay, prism.transform.position, 0);
        AimExact(emitterBlue, relay.transform.position, 0);
        AimExact(relayG, mixer.transform.position, 0);
        AimExact(emitterGreen, relayG.transform.position, 0);

        // --- 4b. Relief : déplace la branche verte si une pente coupe ses rayons ---
        FixGreenBranch(emitterGreen, relayG, mixer);

        // Rayons de la solution (servent à placer dégels et rochers, et à contrôler les obstacles)
        AddBeam(emitterBlue, relay);   AddBeam(relay, prism);   AddBeam(prism, mixer);   AddBeam(mixer, receiverCyan);
        AddBeam(emitterGreen, relayG); AddBeam(relayG, mixer);

        // --- 5. Dégels posés sur des trajets réellement libres, loin des rayons de la solution ---
        int d1 = 6 * sg, d2 = -4 * sg, d3 = 2 * sg, dG = 7 * sg;
        Crystal degel1 = MakeDegel(root,  "Degel_1", emitterBlue,  0,        ref d1, 8f,   Colors.Blue);
        Crystal degel2 = MakeDegel(root,  "Degel_2", relay,        0,        ref d2, 7.2f, Colors.Blue);
        Crystal degel3 = MakeDegel(root,  "Degel_3", prism,        prismOut, ref d3, 9f,   Colors.Blue);
        Crystal degelG = MakeDegel(south, "Degel_G", emitterGreen, 0,        ref dG, 9f,   Colors.Green);

        Crystal[] all = { emitterBlue, relay, prism, mixer, receiverCyan, emitterGreen, relayG, degel1, degel2, degel3, degelG };

        // Orientations de la solution (servent à la vérification)
        Dictionary<Crystal, Quaternion> sol = new Dictionary<Crystal, Quaternion>();
        foreach (Crystal c in new[] { emitterBlue, relay, prism, mixer, emitterGreen, relayG }) sol[c] = c.transform.rotation;

        // --- 6. Glace, mur, rochers (les rochers évitent tous les rayons) ---
        GameObject iceRelay  = MakeIce(relay.transform);
        GameObject icePrism  = MakeIce(prism.transform);
        GameObject iceRelayG = MakeIce(relayG.transform);
        GameObject wall  = MakeIceWall(root, ArcToward(mixer.transform.position, relayG.transform.position, 3.5f), relayG.transform.position);
        GameObject rock1 = MakeRock(root,  "Rocher_1", emitterBlue.transform.position,  prism.transform.position, 0.5f);
        GameObject rock2 = MakeRock(south, "Rocher_2", emitterGreen.transform.position, mixer.transform.position, 0.3f);

        // --- 7. Orientation de départ (en crans depuis la solution) ---
        const int sEB = 3, sRB = 3, sP = -3, sM = 4, sEG = -3, sRG = 2;
        RotateSteps(emitterBlue, sEB);
        RotateSteps(relay, sRB);
        RotateSteps(prism, sP);
        RotateSteps(mixer, sM);
        RotateSteps(emitterGreen, sEG);
        RotateSteps(relayG, sRG);

        // --- 8. Dégel et allumage de la région ---
        Watch(degel1, new[] { relay },  new[] { iceRelay });
        Watch(degel2, new[] { prism },  new[] { icePrism });
        Watch(degel3, new Crystal[0],   new[] { wall });
        Watch(degelG, new[] { relayG }, new[] { iceRelayG });
        ReceiverWatcher final = Watch(receiverCyan, new Crystal[0], new GameObject[0]);

        if (regionLight != null)
        {
            Undo.RecordObject(final, "Region light");
            UnityEventTools.AddStringPersistentListener(final.onLit, regionLight.LightRegion, regionProperty);
            EditorUtility.SetDirty(final);
        }
        else Debug.LogWarning($"{name} : aucun RegionLight trouvé, branche On Lit de Receiver_Cyan à la main.");

        // --- 9. Contrôle géométrique des obstacles (taille réelle des colliders) ---
        Physics.SyncTransforms();
        if (rock1 != null) CheckClear(rock1, 1.6f, null);
        if (rock2 != null) CheckClear(rock2, 1.6f, null);
        CheckClear(wall, 2f, $"{relayG.name} → {mixer.name}");
        foreach (Crystal c in all) CheckClear(c.gameObject, 0.8f, null);

        // --- 10. Vérification avec le vrai BeamManager ---
        int errors = 0;
        StringBuilder report = new StringBuilder();
        void Pose(Crystal c, int n) => c.transform.rotation = sol[c] * Quaternion.Euler(0f, n * StepOf(c), 0f);
        void Run() { Physics.SyncTransforms(); manager.Recompute(); }
        void Expect(bool ok, string msg) { if (ok) return; errors++; report.AppendLine("✗ " + msg); }
        void ExpectBeam(bool ok, string msg, Crystal src, int output, Crystal dst)
        {
            if (ok) return;
            errors++;
            report.AppendLine("✗ " + msg);
            Reaches(src, output, dst, out string why);
            report.AppendLine($"   ↳ rayon {src.name} → {dst.name} : {why}");
        }
        int Turn(Crystal c) => Mathf.Max(1, Mathf.RoundToInt(360f / StepOf(c)));

        typeof(BeamManager).GetMethod("RefreshCrystals", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(manager, null);

        // Étape 1
        Pose(emitterBlue, d1); Pose(relay, sRB); Pose(prism, sP); Pose(mixer, sM); Pose(emitterGreen, sEG); Pose(relayG, sRG);
        Run();
        ExpectBeam(degel1.IsSatisfied, $"Étape 1 : Degel_1 reçoit {degel1.ReceivedColor} au lieu de Blue.", emitterBlue, 0, degel1);

        // Étape 2
        Pose(emitterBlue, 0); Pose(relay, d2);
        Run();
        ExpectBeam(relay.OutputColor == Colors.Blue, $"Étape 2 : Relay_B ne relaie pas (reçoit {relay.ReceivedColor}).", emitterBlue, 0, relay);
        ExpectBeam(degel2.IsSatisfied, $"Étape 2 : Degel_2 reçoit {degel2.ReceivedColor} au lieu de Blue.", relay, 0, degel2);

        // Étape 3
        Pose(relay, 0); Pose(prism, d3);
        Run();
        ExpectBeam(prism.OutputColor == Colors.Blue, $"Étape 3 : Prism_B ne s'allume pas (reçoit {prism.ReceivedColor}).", relay, 0, prism);
        ExpectBeam(degel3.IsSatisfied, $"Étape 3 : Degel_3 reçoit {degel3.ReceivedColor} au lieu de Blue.", prism, prismOut, degel3);

        // Tour complet du prisme : Relay_B doit rester allumé à chaque cran
        bool relayOn = relay.OutputColor == Colors.Blue;
        for (int k = 0; k < Turn(prism); k++)
        {
            Pose(prism, k); Run();
            if (relayOn && relay.OutputColor != Colors.Blue)
                Expect(false, $"Boucle : Prism_B au cran {k} (depuis la solution) éteint Relay_B.");
        }

        // Le mur doit bloquer le vert tant qu'il est là
        Pose(prism, 0); Pose(emitterGreen, 0); Pose(relayG, 0); Pose(mixer, 0);
        Run();
        if (mixer.OutputColor == Colors.Cyan) report.AppendLine("⚠ Le Mur_Glace ne bloque pas le vert : l'étape 3 devient facultative.");

        // Raccourci : avec le mur, aucun cran d'Emitter_Green ne doit envoyer le vert directement au Mixer
        Pose(relayG, sRG);
        for (int k = 0; k < Turn(emitterGreen); k++)
        {
            Pose(emitterGreen, k); Run();
            if ((mixer.ReceivedColor & Colors.Green) != 0 && relayG.OutputColor != Colors.Green)
            {
                report.AppendLine($"⚠ Raccourci : Emitter_Green au cran {k} envoie le vert directement au Mixer (le mur ne sert plus).");
                break;
            }
        }
        Pose(emitterGreen, 0); Pose(relayG, 0);

        // Étape 4
        wall.SetActive(false);
        Pose(emitterGreen, dG); Pose(mixer, sM);
        Run();
        ExpectBeam(degelG.IsSatisfied, $"Étape 4 : Degel_G reçoit {degelG.ReceivedColor} au lieu de Green.", emitterGreen, 0, degelG);

        // Étape 5 : final
        Pose(emitterGreen, 0); Pose(relayG, 0); Pose(mixer, 0);
        Run();
        ExpectBeam(relayG.OutputColor == Colors.Green, $"Final : Relay_G ne relaie pas (reçoit {relayG.ReceivedColor}).", emitterGreen, 0, relayG);
        ExpectBeam((mixer.ReceivedColor & Colors.Blue) != 0, $"Final : le bleu de Prism_B n'arrive pas au Mixer (il reçoit {mixer.ReceivedColor}).", prism, prismOut, mixer);
        ExpectBeam((mixer.ReceivedColor & Colors.Green) != 0, $"Final : le vert de Relay_G n'arrive pas au Mixer (il reçoit {mixer.ReceivedColor}).", relayG, 0, mixer);
        Expect(mixer.OutputColor == Colors.Cyan, $"Final : Mixer_Cyan ne sort pas de Cyan (reçoit {mixer.ReceivedColor}, il faut Blue + Green, 2 rayons exactement).");
        ExpectBeam(receiverCyan.IsSatisfied, $"Final : Receiver_Cyan reçoit {receiverCyan.ReceivedColor} au lieu de Cyan.", mixer, 0, receiverCyan);
        string forced = ForcedOff(all);
        Expect(forced.Length == 0, $"Final : cristaux éteints de force par le BeamManager (boucle instable) : {forced}.");

        // Tour complet du mixer : seuls les vrais retours de rayon sont signalés
        bool pOn = prism.OutputColor == Colors.Blue, rOn = relay.OutputColor == Colors.Blue, gOn = relayG.OutputColor == Colors.Green;
        for (int k = 0; k < Turn(mixer); k++)
        {
            Pose(mixer, k); Run();
            string off = "";
            if (pOn && prism.OutputColor != Colors.Blue) off += " Prism_B";
            if (rOn && relay.OutputColor != Colors.Blue) off += " Relay_B";
            if (gOn && relayG.OutputColor != Colors.Green) off += " Relay_G";
            if (off.Length > 0) Expect(false, $"Boucle : Mixer_Cyan au cran {k} (depuis la solution) éteint{off}.");
        }

        // Retour à l'état de départ
        wall.SetActive(true);
        Pose(emitterBlue, sEB); Pose(relay, sRB); Pose(prism, sP); Pose(mixer, sM); Pose(emitterGreen, sEG); Pose(relayG, sRG);
        Run();

        Undo.CollapseUndoOperations(group);

        // --- 11. Solution + rapport ---
        float st = StepOf(relay), stG = StepOf(relayG);
        StringBuilder solText = new StringBuilder();
        solText.AppendLine("=== Solution région Bleue — sens vus du dessus (horaire = menu Rotate) ===");
        solText.AppendLine($"1. Emitter_Blue : {Moves(d1 - sEB, st)} → Degel_1 (Relay_B dégèle)");
        solText.AppendLine($"2. Emitter_Blue : {Moves(-d1, st)} → Relay_B");
        solText.AppendLine($"3. Relay_B : {Moves(d2 - sRB, st)} → Degel_2 (Prism_B dégèle)");
        solText.AppendLine($"4. Relay_B : {Moves(-d2, st)} → Prism_B");
        solText.AppendLine($"5. Prism_B : {Moves(d3 - sP, st)} → Degel_3 (le mur fond)");
        solText.AppendLine($"6. Prism_B : {Moves(-d3, st)} → Mixer_Cyan");
        solText.AppendLine($"7. Emitter_Green : {Moves(dG - sEG, st)} → Degel_G (Relay_G dégèle)");
        solText.AppendLine($"8. Emitter_Green : {Moves(-dG, st)} → Relay_G");
        solText.AppendLine($"9. Relay_G : {Moves(-sRG, stG)} → Mixer_Cyan");
        solText.AppendLine($"10. Mixer_Cyan : {Moves(-sM, st)} → Receiver_Cyan");
        Debug.Log(solText.ToString());

        if (errors == 0) Debug.Log($"Vérification région Bleue : OK, la solution fonctionne avec le BeamManager.\n{report}");
        else Debug.LogError($"Vérification région Bleue : {errors} problème(s).\n{report}");
    }

    // ---------- Relief ----------

    // Si une pente coupe Emitter_Green → Relay_G ou Relay_G → Mixer, cherche une meilleure position :
    // Relay_G glisse sur la ligne Mixer–Relay_G (garde l'anti-retour), Emitter_Green pivote autour de Relay_G.
    private void FixGreenBranch(Crystal eg, Crystal relayG, Crystal mixer)
    {
        Physics.SyncTransforms();
        if (Reaches(eg, 0, relayG, out _) && Reaches(relayG, 0, mixer, out _)) return;

        Undo.RecordObject(relayG.transform, "Relief");
        Undo.RecordObject(eg.transform, "Relief");
        Vector3 rgPos = relayG.transform.position; Quaternion rgRot = relayG.transform.rotation;
        Vector3 egPos = eg.transform.position;     Quaternion egRot = eg.transform.rotation;
        Vector3 rm = Radial(mixer.transform.position);
        Vector3 slideAxis = Vector3.Cross(rm, Radial(rgPos)).normalized;
        float R = (rgPos - center).magnitude;

        float[] slides = { 0f, -1f, 1f, -2f, 2f, -3f, 3f, -4f, 4f, -5f, 5f, -6f, 6f };
        float[] swings = { 0f, 10f, -10f, 20f, -20f, 30f, -30f };

        foreach (float slide in slides)
            foreach (float swing in swings)
            {
                relayG.transform.SetPositionAndRotation(rgPos, rgRot);
                if (slide != 0f)
                    relayG.transform.position = center + Quaternion.AngleAxis(slide / R * Mathf.Rad2Deg, slideAxis) * (rgPos - center);
                relayG.AlignToPlanet(false);

                eg.transform.SetPositionAndRotation(egPos, egRot);
                if (swing != 0f)
                    eg.transform.position = center + Quaternion.AngleAxis(swing, Radial(relayG.transform.position)) * (egPos - center);
                eg.AlignToPlanet(false);

                AimExact(relayG, mixer.transform.position, 0);
                AimExact(eg, relayG.transform.position, 0);
                Physics.SyncTransforms();

                if (Reaches(eg, 0, relayG, out _) && Reaches(relayG, 0, mixer, out _))
                {
                    Debug.Log($"Relief : Relay_G déplacé de {slide:+0;-0;0} m sur la ligne du Mixer, Emitter_Green pivoté de {swing:+0;-0;0}° autour de Relay_G.");
                    return;
                }
            }

        relayG.transform.SetPositionAndRotation(rgPos, rgRot);
        eg.transform.SetPositionAndRotation(egPos, egRot);
        Physics.SyncTransforms();
        Debug.LogWarning("Relief : aucune position trouvée pour la branche verte. Augmente ou diminue South Angle puis reconstruis.");
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

    // Vrai si la sortie « output » de src, tournée de « steps » crans, parcourt « dist » mètres sans pente ni obstacle.
    private bool PathFree(Crystal src, int output, int steps, float dist)
    {
        float s = StepOf(src);
        Quaternion keep = src.transform.rotation;
        src.transform.rotation = keep * Quaternion.Euler(0f, steps * s, 0f);
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

    private Vector3 Local(float x, float z) => new Vector3((mirrorX ? -x : x) * spacing, 0f, z * spacing);
    private Vector3 At(Transform frame, float x, float z) => frame.TransformPoint(Local(x, z));
    private Vector3 Radial(Vector3 p) => (p - center).normalized;

    private static SerializedProperty Prop(SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) Debug.LogError($"Champ « {field} » introuvable sur {so.targetObject.name}.");
        return p;
    }

    private Crystal MakeCrystal(Transform parent, GameObject prefab, string objName, Vector3 worldPos, CrystalType type,
                                Colors emitted, Colors target, bool rotatable, bool countsForVictory)
    {
        GameObject go = PrefabUtility.IsPartOfPrefabAsset(prefab)
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)
            : Instantiate(prefab, parent);
        Undo.RegisterCreatedObjectUndo(go, objName);
        go.name = objName;
        go.SetActive(true);
        go.transform.SetPositionAndRotation(worldPos, parent.rotation);

        Crystal c = go.GetComponent<Crystal>();
        SerializedObject so = new SerializedObject(c);
        Prop(so, "crystalType").intValue = (int)type;
        Prop(so, "emittedColor").intValue = (int)emitted;
        Prop(so, "targetColor").intValue = (int)target;
        Prop(so, "rotatable").boolValue = rotatable;
        Prop(so, "countsForVictory").boolValue = countsForVictory;
        Prop(so, "startStep").intValue = 0;        // le mélange est appliqué par le constructeur
        Prop(so, "stepAngle").floatValue = 15f;
        Prop(so, "hintColor").intValue = (int)Colors.None;
        Prop(so, "firstTarget").objectReferenceValue = null;
        Prop(so, "secondTarget").objectReferenceValue = null;
        Prop(so, "aimTarget").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();

        c.AlignToPlanet(false);
        return c;
    }

    // Choisit le cran et la distance du dégel : trajet réellement libre (relief, obstacles),
    // dégel loin des rayons de la solution. « steps » reçoit le cran retenu.
    private Crystal MakeDegel(Transform parent, string objName, Crystal source, int output, ref int steps, float dist, Colors color)
    {
        int s0 = steps;
        int sign = s0 >= 0 ? 1 : -1;
        int[] stepTry = { s0, s0 + sign, s0 - sign, s0 + 2 * sign, s0 - 2 * sign, s0 + 3 * sign, s0 - 3 * sign, -s0 };
        float[] distTry = { 1f, 0.8f, 1.2f, 0.65f, 0.5f };
        int full = Mathf.RoundToInt(360f / StepOf(source));
        Vector3 start = source.BeamStart();
        Physics.SyncTransforms();

        Vector3 bestPos = GridTarget(source, output, s0, dist * spacing);
        int bestSteps = s0;
        float bestScore = -1f;
        bool anyFree = false;

        foreach (int st in stepTry)
        {
            int m = ((st % full) + full) % full;
            if (Mathf.Abs(st) < 2 || m == 0) continue;
            foreach (float f in distTry)
            {
                float d = dist * spacing * f;
                if (!PathFree(source, output, st, d)) continue;
                anyFree = true;
                Vector3 p = GridTarget(source, output, st, d);
                float score = Mathf.Min(BeamClearance(p), PathClearance(start, p, source) + 1f);
                if (score > bestScore) { bestScore = score; bestPos = p; bestSteps = st; }
                if (score >= degelClearance) goto Found;
            }
        }
        if (!anyFree) Debug.LogWarning($"{objName} : aucun trajet libre trouvé depuis {source.name} (relief), position par défaut.");
        else Debug.LogWarning($"{objName} : pas de place à {degelClearance} m des rayons, meilleure position à {bestScore:0.0} m.");
    Found:
        steps = bestSteps;
        Crystal dg = MakeCrystal(parent, receiverPrefab, objName, bestPos, CrystalType.Receiver, Colors.None, color, false, false);
        placed.Add(dg);
        AddBeam(source, dg);
        return dg;
    }

    // Distance horizontale entre deux points (composante radiale retirée).
    private float HDist(Vector3 a, Vector3 b)
    {
        Vector3 d = a - b;
        Vector3 r = Radial(b);
        return (d - r * Vector3.Dot(d, r)).magnitude;
    }

    // Distance horizontale minimale entre un point et les rayons enregistrés.
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

    // Distance minimale entre l'arc a→b et les cristaux placés (hors source).
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

    private static void SetHint(Crystal c, Colors color)
    {
        SerializedObject so = new SerializedObject(c);
        Prop(so, "hintColor").intValue = (int)color;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStep(Crystal c, float angle)
    {
        SerializedObject so = new SerializedObject(c);
        Prop(so, "stepAngle").floatValue = angle;
        so.ApplyModifiedPropertiesWithoutUndo();
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

    // Tourne le cristal pour que sa sortie « index » suive le grand cercle vers target (départ = BeamStart).
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

    // Vise la cible avec la sortie dont l'autre rayon part le plus vers le pôle. Renvoie l'index de la sortie utilisée.
    private int AimPrism(Crystal prism, Vector3 target, Vector3 poleHint)
    {
        float split = new SerializedObject(prism).FindProperty("splitAngle").floatValue;
        float step = StepOf(prism);
        float rest = Mathf.Repeat(2f * split, step);
        if (rest > 0.01f && rest < step - 0.01f)
            Debug.LogWarning($"{prism.name} : 2 × Split Angle ({2f * split}°) n'est pas un multiple du Step Angle ({step}°), l'anti-retour sera approximatif.");

        int best = 0;
        float bestScore = float.MinValue;
        for (int i = 0; i < 2; i++)
        {
            AimExact(prism, target, i);
            Vector3[] hs = prism.GetHeadings();
            Vector3 r = Radial(prism.BeamStart());
            float score = Vector3.Dot(Vector3.ProjectOnPlane(hs[1 - i], r).normalized,
                                      Vector3.ProjectOnPlane(poleHint, r).normalized);
            if (score > bestScore) { bestScore = score; best = i; }
        }
        AimExact(prism, target, best);
        return best;
    }

    // Écart (dans [-cran/2, +cran/2]) entre la grille des sorties de source et la direction de pos.
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

    // Déplace target autour de source (même distance) pour qu'il soit à mi-cran de toutes les sorties de source.
    private void MoveToHalfStep(Crystal source, int output, Crystal target)
    {
        float s = StepOf(source);
        float phase = GridPhase(source, output, target.transform.position);
        float wanted = phase >= 0f ? s * 0.5f : -s * 0.5f;
        MoveAround(target, Radial(source.BeamStart()), wanted - phase);
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

    // Met target à mi-cran de primary, et le plus loin possible des crans de secondary.
    private void AvoidBoth(Crystal primary, int primaryOut, Crystal secondary, Crystal target)
    {
        MoveToHalfStep(primary, primaryOut, target);
        if (Mathf.Abs(GridPhase(secondary, 0, target.transform.position)) >= 4f) return;

        Vector3 axis = Radial(primary.BeamStart());
        float s = StepOf(primary);
        Vector3 offset = target.transform.position - center;
        float best = -1f, bestAngle = 0f;
        foreach (float k in new[] { 0f, s, -s, 2f * s, -2f * s })
        {
            Vector3 pos = center + Quaternion.AngleAxis(k, axis) * offset;
            float ph = Mathf.Abs(GridPhase(secondary, 0, pos));
            if (ph > best) { best = ph; bestAngle = k; }
        }
        MoveAround(target, axis, bestAngle);
    }

    // Point atteint par la sortie « index » après « steps » crans, à « dist » mètres le long du grand cercle.
    private Vector3 GridTarget(Crystal c, int index, int steps, float dist)
    {
        float step = StepOf(c);
        Quaternion keep = c.transform.rotation;
        c.transform.rotation = keep * Quaternion.Euler(0f, steps * step, 0f);
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

    private float ArcLength(Vector3 a, Vector3 b)
    {
        Vector3 ra = a - center, rb = b - center;
        return Vector3.Angle(ra, rb) * Mathf.Deg2Rad * (ra.magnitude + rb.magnitude) * 0.5f;
    }

    private Vector3 ArcToward(Vector3 from, Vector3 to, float dist)
    {
        float l = ArcLength(from, to);
        return ArcPoint(from, to, l > 0f ? Mathf.Clamp01(dist / l) : 0f);
    }

    private void AddBeam(Crystal s, Crystal d) => beams.Add(new Beam
    {
        a = s.BeamStart(), b = d.transform.position,
        label = $"{s.name} → {d.name}", src = s.gameObject, dst = d.gameObject
    });

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

    private void CheckClear(GameObject obstacle, float clearance, string except)
    {
        clearance = Mathf.Max(clearance, Footprint(obstacle) + 0.3f);
        Vector3 o = obstacle.transform.position;
        foreach (Beam bm in beams)
        {
            if (bm.src == obstacle || bm.dst == obstacle || bm.label == except) continue;
            float min = DistToBeam(o, bm.a, bm.b);
            if (min < clearance)
                Debug.LogWarning($"{obstacle.name} est à {min:0.0} m du rayon {bm.label} (taille {clearance:0.0} m) : il risque de le bloquer.", obstacle);
        }
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

    private GameObject MakeIce(Transform crystal)
    {
        Bounds b = new Bounds(crystal.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in crystal.GetComponentsInChildren<Renderer>())
        {
            if (r is LineRenderer || r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        float size = any ? Mathf.Max(b.size.x, b.size.y, b.size.z) * 1.15f : 1.5f;

        GameObject ice = GameObject.CreatePrimitive(PrimitiveType.Cube);
        DestroyImmediate(ice.GetComponent<Collider>());   // ne bloque ni les rayons ni le joueur
        Undo.RegisterCreatedObjectUndo(ice, "Ice");
        ice.name = "Glace";
        ice.transform.SetParent(crystal, false);
        ice.transform.position = any ? b.center : crystal.position;
        ice.transform.localRotation = Quaternion.identity;
        Vector3 s = crystal.lossyScale;
        ice.transform.localScale = new Vector3(size / s.x, size / s.y, size / s.z);

        MeshRenderer mr = ice.GetComponent<MeshRenderer>();
        if (iceMaterial != null) mr.sharedMaterial = iceMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ice;
    }

    private GameObject PlaceObstacle(Transform parent, GameObject go, string objName, Vector3 worldPos)
    {
        Undo.RegisterCreatedObjectUndo(go, objName);
        go.name = objName;
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;

        int layer = LayerMask.NameToLayer(obstacleLayer);
        if (layer < 0) Debug.LogWarning($"{name} : layer « {obstacleLayer} » introuvable.");
        else if (layer == LayerMask.NameToLayer("Planete")) Debug.LogWarning($"{name} : l'Obstacle Layer ne doit pas être Planete.");
        else go.layer = layer;

        Vector3 up = Radial(worldPos);
        Vector3 fwd = Vector3.ProjectOnPlane(parent.forward, up);
        go.transform.rotation = Quaternion.LookRotation(fwd.normalized, up);
        return go;
    }

    private GameObject MakeIceWall(Transform parent, Vector3 worldPos, Vector3 faceTarget)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);   // garde son collider : bloque le rayon
        wall.transform.localScale = new Vector3(3f, 4f, 0.6f);
        if (iceMaterial != null) wall.GetComponent<MeshRenderer>().sharedMaterial = iceMaterial;
        PlaceObstacle(parent, wall, "Mur_Glace", worldPos);

        Vector3 up = wall.transform.up;
        Vector3 dir = Vector3.ProjectOnPlane(faceTarget - wall.transform.position, up);
        if (dir.sqrMagnitude > 1e-4f) wall.transform.rotation = Quaternion.LookRotation(dir.normalized, up);

        Ground(wall.transform);
        return wall;
    }

    // Pose un rocher sur la ligne de raccourci a→b (vers « preferred »), à l'écart de tous les rayons et cristaux.
    // Renvoie null s'il n'y a pas de place.
    private GameObject MakeRock(Transform parent, string objName, Vector3 a, Vector3 b, float preferred)
    {
        GameObject rock;
        if (rockPrefab != null)
        {
            rock = PrefabUtility.IsPartOfPrefabAsset(rockPrefab)
                ? (GameObject)PrefabUtility.InstantiatePrefab(rockPrefab)
                : Instantiate(rockPrefab);
        }
        else
        {
            rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.transform.localScale = new Vector3(2.2f, 2.4f, 2f);
            if (rockMaterial != null) rock.GetComponent<MeshRenderer>().sharedMaterial = rockMaterial;
        }

        PlaceObstacle(parent, rock, objName, ArcPoint(a, b, preferred));
        Physics.SyncTransforms();
        float need = Mathf.Max(1f, Footprint(rock)) + 0.8f;

        float bestF = -1f, bestDelta = float.MaxValue;
        for (float f = 0.15f; f <= 0.851f; f += 0.05f)
        {
            Vector3 p = ArcPoint(a, b, f);
            if (Mathf.Min(BeamClearance(p), CrystalClearance(p)) < need) continue;
            float delta = Mathf.Abs(f - preferred);
            if (delta < bestDelta) { bestDelta = delta; bestF = f; }
        }

        if (bestF < 0f)
        {
            Debug.LogWarning($"{objName} non posé : aucune place sur sa ligne à {need:0.0} m des rayons.");
            Undo.DestroyObjectImmediate(rock);
            return null;
        }

        Vector3 pos = ArcPoint(a, b, bestF);
        Vector3 up = Radial(pos);
        rock.transform.position = pos;
        rock.transform.rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), up)
                                * Quaternion.LookRotation(Vector3.ProjectOnPlane(parent.forward, up).normalized, up);
        Ground(rock.transform);
        return rock;
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

    private static ReceiverWatcher Watch(Crystal receiver, Crystal[] unlock, GameObject[] hide)
    {
        ReceiverWatcher w = Undo.AddComponent<ReceiverWatcher>(receiver.gameObject);
        SerializedObject so = new SerializedObject(w);
        Prop(so, "receiver").objectReferenceValue = receiver;

        SerializedProperty u = Prop(so, "crystalsToUnlock");
        u.arraySize = unlock.Length;
        for (int i = 0; i < unlock.Length; i++) u.GetArrayElementAtIndex(i).objectReferenceValue = unlock[i];

        SerializedProperty h = Prop(so, "objectsToHide");
        h.arraySize = hide.Length;
        for (int i = 0; i < hide.Length; i++) h.GetArrayElementAtIndex(i).objectReferenceValue = hide[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        return w;
    }
#endif
}