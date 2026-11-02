using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Outils de placement du grand puzzle. À placer sur Crystal_Puzzle, à côté du BeamManager.
// Les règles (pente, pas, distance, masques) sont lues directement dans le BeamManager.
//  - Check links (exact)        : vérifie chaque liaison exactement comme le BeamManager.
//  - Find best orientation      : tourne tout Crystal_Puzzle, recale les cristaux, puis vérifie.
//  - Find best spot for crystal : déplace UN cristal (Crystal To Move) là où tout passe le mieux.
[RequireComponent(typeof(BeamManager))]
public class PuzzleOrientationFinder : MonoBehaviour
{
    [Header("Estimation rapide (grille)")]
    [Tooltip("Une liaison estimée au-delà de (1 - marge) × pente max compte comme bloquée.")]
    [SerializeField, Range(0f, 0.5f)] private float safetyMargin = 0.15f;
    [SerializeField] private float gridResolution = 0.5f;   // degrés

    [Header("Recherche d'orientation")]
    [SerializeField] private int poleCount = 600;
    [SerializeField] private int twistCount = 48;
    [SerializeField] private int refineIterations = 3000;

    [Header("Recherche d'emplacement (un seul cristal)")]
    [SerializeField] private Crystal crystalToMove;
    [SerializeField] private float searchRadius = 40f;      // degrés autour de sa position actuelle
    [SerializeField] private int spotCandidates = 40000;    // points de la sphère (≈ 1° d'écart)
    [SerializeField] private int exactChecks = 150;         // meilleurs candidats revérifiés exactement
    [SerializeField] private float minSeparation = 8f;      // degrés minimum avec les autres cristaux
    [SerializeField] private float beamClearance = 2.5f;    // degrés minimum entre un cristal et un faisceau qui ne le concerne pas
    [SerializeField] private float minSplit = 15f;          // écart de prisme accepté
    [SerializeField] private float maxSplit = 75f;

    [Header("Raccourcis")]
    [SerializeField] private float rotationStep = 7.5f;     // cran de rotation des cristaux (Q/E)

#if UNITY_EDITOR
    private class Link { public string name; public Vector3[] samples; }

    private class Pair
    {
        public Crystal from;
        public Transform to;
        public Transform prismFirst, prismSecond;   // renseignés si "from" est un prisme
        public string Name => $"{from.name} → {to.name}";
    }

    private struct Exact { public bool reached; public float worst; public string reason; }

    // Règles lues dans le BeamManager
    private Transform planet;
    private Vector3 center;
    private float maxRise, stepLength, maxDistance, probeRadius;
    private int groundLayers, hitLayers;

    // Grille du sol (estimation rapide)
    private float[,] grid;
    private int latCount, lonCount;
    private float meanGround, meanHeight;

    private float BlockLimit => 1f - safetyMargin;

    // ───────────────────────── Vérification exacte ─────────────────────────

    [ContextMenu("Check links (exact)")]
    private void CheckLinks()
    {
        if (!ReadSettings()) return;
        LogExact("[Vérification]", CollectPairs());
    }

        [ContextMenu("Check shortcuts")]
    private void CheckShortcuts()
    {
        if (!ReadSettings()) return;
        Stopwatch sw = Stopwatch.StartNew();
        List<Pair> pairs = CollectPairs();
        Crystal[] all = GetComponentsInChildren<Crystal>(true);

        // Profondeur de chaque cristal dans la chaîne prévue (émetteurs = 0)
        Dictionary<Transform, int> depth = new Dictionary<Transform, int>();
        Queue<Transform> queue = new Queue<Transform>();
        foreach (Crystal c in all)
            if (c.crystalType == CrystalType.Emitter) { depth[c.transform] = 0; queue.Enqueue(c.transform); }
        while (queue.Count > 0)
        {
            Transform t = queue.Dequeue();
            foreach (Pair p in pairs)
                if (p.from.transform == t && !depth.ContainsKey(p.to)) { depth[p.to] = depth[t] + 1; queue.Enqueue(p.to); }
        }

        int steps = Mathf.Max(1, Mathf.RoundToInt(360f / rotationStep));
        List<string> skips = new List<string>();
        List<string> parasites = new List<string>();

        foreach (Crystal c in all)
        {
            if (c.crystalType == CrystalType.Receiver) continue;
            bool fixedDir = !c.rotatable;

            HashSet<Transform> intended = new HashSet<Transform>();
            int intendedDepth = -1;
            foreach (Pair p in pairs)
            {
                if (p.from != c) continue;
                intended.Add(p.to);
                if (depth.TryGetValue(p.to, out int d)) intendedDepth = Mathf.Max(intendedDepth, d);
            }

            Vector3 up = (c.BeamStart() - center).normalized;
            Vector3[] headings = c.GetHeadings();
            Dictionary<Crystal, List<float>> found = new Dictionary<Crystal, List<float>>();

            for (int k = 0; k < (fixedDir ? 1 : steps); k++)
            {
                float angle = k * rotationStep;
                if (angle > 180f) angle -= 360f;
                Quaternion rot = Quaternion.AngleAxis(angle, up);

                foreach (Vector3 h in headings)
                {
                    Crystal hit = TraceHeading(c, rot * h);
                    if (hit == null || intended.Contains(hit.transform)) continue;
                    if (!found.TryGetValue(hit, out List<float> list)) found[hit] = list = new List<float>();
                    if (!list.Contains(angle)) list.Add(angle);
                }
            }

            foreach (KeyValuePair<Crystal, List<float>> kv in found)
            {
                kv.Value.Sort();
                List<string> angles = new List<string>();
                for (int i = 0; i < Mathf.Min(4, kv.Value.Count); i++) angles.Add($"{kv.Value[i]:+0.#;-0.#;0}°");
                if (kv.Value.Count > 4) angles.Add("…");

                bool skip = intendedDepth >= 0 && depth.TryGetValue(kv.Key.transform, out int hd) && hd > intendedDepth;
                string line = $"{c.name} → {kv.Key.name} (à {string.Join(", ", angles)} de la position actuelle)";
                (skip ? skips : parasites).Add(line);
            }
        }

        string msg = $"[Raccourcis] {sw.ElapsedMilliseconds} ms — {skips.Count} sauts d'étape, {parasites.Count} liaisons parasites.";
        if (skips.Count > 0) msg += "\n⚠ SAUTENT DES ÉTAPES (à casser) :\n   " + string.Join("\n   ", skips);
        if (parasites.Count > 0) msg += "\n• Parasites (vérifie les couleurs) :\n   " + string.Join("\n   ", parasites);
        if (skips.Count > 0) Debug.LogWarning(msg); else Debug.Log(msg);
    }

    // Trace un faisceau dans une direction donnée (même règles que le BeamManager) et renvoie le cristal touché.
    private Crystal TraceHeading(Crystal source, Vector3 heading)
    {
        Vector3 start = source.BeamStart();
        Vector3 dir = (start - center).normalized;
        heading = Vector3.ProjectOnPlane(heading, dir);
        if (heading.sqrMagnitude < 1e-8f) return null;
        heading.Normalize();

        float startRadius = (start - center).magnitude;
        if (!Ground(dir, out float groundR)) return null;
        float height = startRadius - groundR;

        Vector3 axis = Vector3.Cross(dir, heading).normalized;
        Quaternion stepRotation = Quaternion.AngleAxis(stepLength / startRadius * Mathf.Rad2Deg, axis);
        Vector3 current = start;
        float travelled = 0f;

        while (travelled < maxDistance)
        {
            Vector3 nextDir = stepRotation * dir;
            if (!Ground(nextDir, out float nextR)) return null;
            if (Mathf.Abs(nextR - groundR) > maxRise) return null;

            Vector3 next = center + nextDir * (nextR + height);
            Vector3 chord = next - current;
            float length = chord.magnitude;

            if (Physics.Raycast(current, chord / length, out RaycastHit hit, length, hitLayers, QueryTriggerInteraction.Ignore))
            {
                Crystal c = hit.collider.GetComponentInParent<Crystal>();
                return c != source ? c : null;
            }

            current = next;
            dir = nextDir;
            groundR = nextR;
            travelled += length;
        }
        return null;
    }

    // ───────────────────────── Orientation d'ensemble ─────────────────────────

    [ContextMenu("Find best orientation")]
    private void FindBestOrientation()
    {
        Stopwatch sw = Stopwatch.StartNew();
        if (!ReadSettings()) return;
        if ((transform.position - center).sqrMagnitude > 0.01f)
        {
            Debug.LogWarning($"{name} : doit être au centre de la planète (position locale 0,0,0).");
            return;
        }
        BuildGrid();
        MeasureHeight();

        List<Pair> pairs = CollectPairs();
        Quaternion toLocal = Quaternion.Inverse(transform.rotation);
        List<Link> links = new List<Link>();
        foreach (Pair p in pairs)
        {
            Link l = BuildLink(toLocal * Dir(p.from.transform), toLocal * Dir(p.to), p.Name);
            if (l != null) links.Add(l);
            else Debug.LogWarning($"{p.Name} : liaison ignorée (cristaux confondus ou opposés).");
        }
        if (links.Count == 0) { Debug.LogWarning($"{name} : aucune liaison trouvée."); return; }

        Quaternion best = transform.rotation;
        Evaluate(best, links, int.MaxValue, out int bestBlocked, out float bestMargin);
        int startBlocked = bestBlocked;

        for (int p = 0; p < poleCount; p++)
        {
            Vector3 pole = Fibonacci(p, poleCount);
            Quaternion baseRot = Quaternion.FromToRotation(Vector3.up, pole);
            for (int t = 0; t < twistCount; t++)
                TryCandidate(Quaternion.AngleAxis(360f * t / twistCount, pole) * baseRot, links, ref best, ref bestBlocked, ref bestMargin);
        }
        for (int i = 0; i < refineIterations; i++)
        {
            float amp = Mathf.Lerp(10f, 0.5f, i / (float)refineIterations);
            TryCandidate(Quaternion.AngleAxis(Random.Range(-amp, amp), Random.onUnitSphere) * best, links, ref best, ref bestBlocked, ref bestMargin);
        }

        UnityEditor.Undo.RegisterFullObjectHierarchyUndo(gameObject, "Find best orientation");
        transform.rotation = best;
        SnapAll();

        Debug.Log($"[Orientation] {sw.ElapsedMilliseconds} ms — estimation : {startBlocked} → {bestBlocked} liaisons bloquées. Cristaux recalés sur le terrain.");
        LogExact("[Orientation] Vérification exacte :", pairs);
    }

    // ───────────────────────── Emplacement d'un cristal ─────────────────────────

    [ContextMenu("Find best spot for crystal")]
    private void FindBestSpot()
    {
        if (crystalToMove == null) { Debug.LogWarning($"{name} : Crystal To Move non assigné."); return; }
        Stopwatch sw = Stopwatch.StartNew();
        if (!ReadSettings()) return;
        BuildGrid();
        MeasureHeight();

        Transform mover = crystalToMove.transform;
        List<Pair> pairs = CollectPairs();

        List<Pair> involved = new List<Pair>();
        List<Vector3> otherPath = new List<Vector3>();
        foreach (Pair p in pairs)
        {
            if (p.from.transform == mover || p.to == mover) { involved.Add(p); continue; }
            Link l = BuildLink(Dir(p.from.transform), Dir(p.to), "");
            if (l != null) otherPath.AddRange(l.samples);
        }
        if (involved.Count == 0) { Debug.LogWarning($"{mover.name} : aucune liaison ne le concerne."); return; }

        List<Transform> others = new List<Transform>();
        foreach (Crystal c in GetComponentsInChildren<Crystal>(true)) if (c.transform != mover) others.Add(c.transform);
        foreach (ResetCrystal r in GetComponentsInChildren<ResetCrystal>(true)) others.Add(r.transform);
        Vector3[] otherDirs = new Vector3[others.Count];
        for (int i = 0; i < others.Count; i++) otherDirs[i] = Dir(others[i]);

        float cosSearch = Mathf.Cos(searchRadius * Mathf.Deg2Rad);
        float cosSep = Mathf.Cos(minSeparation * Mathf.Deg2Rad);
        float cosClear = Mathf.Cos(beamClearance * Mathf.Deg2Rad);
        Vector3 current = Dir(mover);

        // Phase 1 : estimation rapide sur la grille
        bool GridSpot(Vector3 d, out int blocked, out float margin)
        {
            blocked = 0; margin = 0f;
            foreach (Vector3 o in otherDirs) if (Vector3.Dot(d, o) > cosSep) return false;
            foreach (Vector3 s in otherPath) if (Vector3.Dot(d, s) > cosClear) return false;

            foreach (Pair p in involved)
            {
                Vector3 a = p.from.transform == mover ? d : Dir(p.from.transform);
                Vector3 b = p.to == mover ? d : Dir(p.to);

                if (p.prismFirst != null && p.prismSecond != null)
                {
                    Vector3 f = p.prismFirst == mover ? d : Dir(p.prismFirst);
                    Vector3 s = p.prismSecond == mover ? d : Dir(p.prismSecond);
                    if (!SplitOk(a, f, s)) return false;
                }

                Link l = BuildLink(a, b, p.Name);
                if (l == null) return false;

                for (int k = 1; k < l.samples.Length - 1; k++)
                    for (int o = 0; o < others.Count; o++)
                    {
                        if (others[o] == p.from.transform || others[o] == p.to) continue;
                        if (Vector3.Dot(l.samples[k], otherDirs[o]) > cosClear) return false;
                    }

                float w = TraceLink(Quaternion.identity, l);
                if (w > BlockLimit) blocked++;
                margin += Mathf.Min(w, 5f);
            }
            return true;
        }

        List<(Vector3 dir, int blocked, float score)> ranked = new List<(Vector3, int, float)>();
        for (int i = 0; i < spotCandidates; i++)
        {
            Vector3 d = Fibonacci(i, spotCandidates);
            if (Vector3.Dot(d, current) < cosSearch) continue;
            if (!GridSpot(d, out int blocked, out float margin)) continue;
            ranked.Add((d, blocked, margin + 0.01f * Vector3.Angle(d, current)));
        }
        if (ranked.Count == 0)
        {
            Debug.LogWarning($"{mover.name} : aucun emplacement valide. Augmente Search Radius, ou baisse Min Separation / Beam Clearance.");
            return;
        }
        ranked.Sort((a, b) => a.blocked != b.blocked ? a.blocked.CompareTo(b.blocked) : a.score.CompareTo(b.score));

        // Phase 2 : vérification exacte des meilleurs candidats (cristal réellement déplacé et recalé)
        UnityEditor.Undo.RegisterFullObjectHierarchyUndo(gameObject, "Find best spot");
        Vector3 originalPos = mover.position;
        Quaternion originalRot = mover.rotation;
        float radius = (originalPos - center).magnitude;

        int startBlocked = CountExact(pairs, out float startWorst);
        int bestIndex = -1, bestBlocked = int.MaxValue;
        float bestWorst = float.MaxValue;
        int checks = Mathf.Min(exactChecks, ranked.Count);

        for (int i = 0; i < checks; i++)
        {
            Place(ranked[i].dir, radius);
            int blocked = CountExact(pairs, out float worst);
            if (blocked < bestBlocked || (blocked == bestBlocked && worst < bestWorst))
            {
                bestIndex = i; bestBlocked = blocked; bestWorst = worst;
            }
        }

        if (bestBlocked > startBlocked || (bestBlocked == startBlocked && bestWorst >= startWorst))
        {
            mover.position = originalPos;
            mover.rotation = originalRot;
            Physics.SyncTransforms();
            Debug.LogWarning($"{mover.name} : aucun emplacement meilleur que l'actuel ({startBlocked} liaisons bloquées). Rien n'a changé (Ctrl+Z si besoin). " +
                             "Essaie d'augmenter Search Radius ou Exact Checks.");
            return;
        }

        Place(ranked[bestIndex].dir, radius);
        float moved = Vector3.Angle(current, ranked[bestIndex].dir);

        List<string> todo = new List<string>();
        foreach (Pair p in pairs)
        {
            if (p.to != mover) continue;
            string s = p.prismFirst != null ? $"« Aim prism at both targets » sur {p.from.name}" : $"« Aim at target » sur {p.from.name}";
            if (!todo.Contains(s)) todo.Add(s);
        }
        foreach (Pair p in pairs)
        {
            if (p.from.transform != mover) continue;
            string s = p.prismFirst != null ? $"« Aim prism at both targets » sur {mover.name}" : $"« Aim at target » sur {mover.name}";
            if (!todo.Contains(s)) todo.Add(s);
        }
        todo.Add("Recompute");

        Debug.Log($"[Emplacement] {sw.ElapsedMilliseconds} ms — {mover.name} déplacé de {moved:F1}° (≈ {moved * Mathf.Deg2Rad * meanGround:F1} unités) et recalé — " +
                  $"liaisons bloquées (exact, toutes) : {startBlocked} → {bestBlocked}.\nÀ faire : {string.Join(", puis ", todo)}.");
        LogExact("[Emplacement] Vérification exacte (état après re-visée) :", pairs);
    }

    private void Place(Vector3 dir, float radius)
    {
        crystalToMove.transform.position = center + dir * radius;
        crystalToMove.AlignToPlanet(false);
        Physics.SyncTransforms();
    }

    // ───────────────────────── Règles du BeamManager ─────────────────────────

    private bool ReadSettings()
    {
        BeamManager manager = GetComponent<BeamManager>();
        planet = manager.PlanetTransform;
        if (planet == null) { Debug.LogWarning($"{name} : Planet Transform non assigné dans le BeamManager."); return false; }
        center = planet.position;

        UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(manager);
        UnityEditor.SerializedProperty slope = so.FindProperty("maxSlopeAngle");
        UnityEditor.SerializedProperty stepProp = so.FindProperty("stepLength");
        UnityEditor.SerializedProperty dist = so.FindProperty("maxDistance");
        UnityEditor.SerializedProperty groundProp = so.FindProperty("groundMask");
        UnityEditor.SerializedProperty beamProp = so.FindProperty("beamMask");
        if (slope == null || stepProp == null || dist == null || groundProp == null || beamProp == null)
        {
            Debug.LogWarning($"{name} : champs du BeamManager introuvables (maxSlopeAngle, stepLength, maxDistance, groundMask, beamMask).");
            return false;
        }

        stepLength = stepProp.floatValue;
        maxDistance = dist.floatValue;
        maxRise = Mathf.Tan(slope.floatValue * Mathf.Deg2Rad) * stepLength;
        groundLayers = groundProp.intValue != 0 ? groundProp.intValue : LayerMask.GetMask("Planete");
        hitLayers = beamProp.intValue & ~groundLayers;

        Physics.SyncTransforms();

        probeRadius = 0f;
        foreach (Collider col in planet.GetComponentsInChildren<Collider>())
        {
            if ((groundLayers & (1 << col.gameObject.layer)) == 0) continue;
            Bounds b = col.bounds;
            probeRadius = Mathf.Max(probeRadius, Vector3.Distance(center, b.center) + b.extents.magnitude);
        }
        if (probeRadius <= 0f) { Debug.LogWarning($"{name} : aucun collider de planète dans le Ground Mask du BeamManager."); return false; }
        probeRadius += 1f;
        return true;
    }

    private bool Ground(Vector3 dir, out float r) =>
        TerrainSnap.GroundRadius(planet, dir, probeRadius, groundLayers, out r, out _);

    private Vector3 Dir(Transform t) => (t.position - center).normalized;

    private void SnapAll()
    {
        foreach (Crystal c in GetComponentsInChildren<Crystal>()) c.AlignToPlanet(false);
        foreach (ResetCrystal r in GetComponentsInChildren<ResetCrystal>()) r.AlignToPlanet();
        Physics.SyncTransforms();
    }

    // Même algorithme que BeamManager.TraceBeam, mais en visant directement la cible (= état après re-visée).
    private Exact TraceExact(Crystal source, Transform target)
    {
        Vector3 start = source.BeamStart();
        Vector3 dir = (start - center).normalized;
        Vector3 targetDir = Dir(target);
        Vector3 heading = Vector3.ProjectOnPlane(target.position - center, dir);
        if (heading.sqrMagnitude < 1e-8f) return new Exact { reason = "cible à la verticale de la source" };
        heading.Normalize();

        float startRadius = (start - center).magnitude;
        if (!Ground(dir, out float groundR)) return new Exact { reason = "pas de sol sous la source" };
        float height = startRadius - groundR;

        Vector3 axis = Vector3.Cross(dir, heading).normalized;
        Quaternion stepRotation = Quaternion.AngleAxis(stepLength / startRadius * Mathf.Rad2Deg, axis);

        Vector3 current = start;
        float travelled = 0f, worst = 0f;
        float prevRemain = Vector3.Angle(dir, targetDir);

        while (travelled < maxDistance)
        {
            Vector3 nextDir = stepRotation * dir;

            if (!Ground(nextDir, out float nextR))
                return new Exact { worst = worst, reason = "trou (pas de sol)" };

            float ratio = Mathf.Abs(nextR - groundR) / maxRise;
            worst = Mathf.Max(worst, ratio);
            if (ratio > 1f)
                return new Exact { worst = worst, reason = nextR > groundR ? "paroi trop raide (montée)" : "falaise trop raide (descente)" };

            Vector3 next = center + nextDir * (nextR + height);
            Vector3 chord = next - current;
            float length = chord.magnitude;

            if (Physics.Raycast(current, chord / length, out RaycastHit hit, length, hitLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(target)) return new Exact { reached = true, worst = worst };
                Crystal other = hit.collider.GetComponentInParent<Crystal>();
                return new Exact { worst = worst, reason = $"intercepté par {(other != null ? other.name : hit.collider.name)}" };
            }

            float remain = Vector3.Angle(nextDir, targetDir);
            if (remain > prevRemain) return new Exact { worst = worst, reason = "passe à côté de la cible (hauteur ?)" };
            prevRemain = remain;

            current = next;
            dir = nextDir;
            groundR = nextR;
            travelled += length;
        }
        return new Exact { worst = worst, reason = $"trop long (> {maxDistance})" };
    }

    private int CountExact(List<Pair> pairs, out float sumWorst)
    {
        int blocked = 0; sumWorst = 0f;
        foreach (Pair p in pairs)
        {
            Exact e = TraceExact(p.from, p.to);
            if (!e.reached) blocked++;
            sumWorst += Mathf.Min(e.worst, 2f);
        }
        return blocked;
    }

    private void LogExact(string title, List<Pair> pairs)
    {
        List<(string text, float sort)> lines = new List<(string, float)>();
        int blocked = 0;
        foreach (Pair p in pairs)
        {
            Exact e = TraceExact(p.from, p.to);
            string text = e.reached
                ? $"✔ {p.Name} : atteint — pente max {e.worst * 100f:F0} %"
                : $"✘ {p.Name} : {e.reason} — pente max {e.worst * 100f:F0} %";
            if (!e.reached) blocked++;
            lines.Add((text, e.reached ? e.worst : 100f + e.worst));
        }
        lines.Sort((a, b) => b.sort.CompareTo(a.sort));

        List<string> texts = new List<string>();
        foreach (var l in lines) texts.Add(l.text);
        string msg = $"{title} {pairs.Count - blocked}/{pairs.Count} liaisons OK.\n   " + string.Join("\n   ", texts);
        if (blocked > 0) Debug.LogWarning(msg); else Debug.Log(msg);
    }

    // ───────────────────────── Estimation rapide (grille) ─────────────────────────

    private List<Pair> CollectPairs()
    {
        List<Pair> pairs = new List<Pair>();
        foreach (Crystal c in GetComponentsInChildren<Crystal>(true))
        {
            UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(c);
            Transform first = RefTransform(so, "firstTarget");
            Transform second = RefTransform(so, "secondTarget");

            if (first != null || second != null)
            {
                if (first != null) pairs.Add(new Pair { from = c, to = first, prismFirst = first, prismSecond = second });
                if (second != null) pairs.Add(new Pair { from = c, to = second, prismFirst = first, prismSecond = second });
            }
            else
            {
                Transform aim = RefTransform(so, "aimTarget");
                if (aim != null) pairs.Add(new Pair { from = c, to = aim });
            }
        }
        return pairs;
    }

    private static Transform RefTransform(UnityEditor.SerializedObject so, string field)
    {
        UnityEditor.SerializedProperty prop = so.FindProperty(field);
        if (prop == null || prop.propertyType != UnityEditor.SerializedPropertyType.ObjectReference) return null;
        Object obj = prop.objectReferenceValue;
        if (obj is Component comp) return comp.transform;
        if (obj is GameObject go) return go.transform;
        return null;
    }

    private bool SplitOk(Vector3 prism, Vector3 f, Vector3 s)
    {
        Vector3 hf = Vector3.ProjectOnPlane(f, prism);
        Vector3 hs = Vector3.ProjectOnPlane(s, prism);
        if (hf.sqrMagnitude < 1e-8f || hs.sqrMagnitude < 1e-8f) return false;
        float half = Vector3.Angle(hf, hs) * 0.5f;
        return half >= minSplit && half <= maxSplit;
    }

    private Link BuildLink(Vector3 a, Vector3 b, string linkName)
    {
        float dot = Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f);
        float angle = Mathf.Acos(dot);
        if (angle < 1e-4f || angle > Mathf.PI - 1e-3f) return null;

        float angleStep = stepLength / (meanGround + meanHeight);
        int n = Mathf.Max(1, Mathf.CeilToInt(angle / angleStep));
        Vector3 perp = (b - a * dot).normalized;

        Vector3[] samples = new Vector3[n + 1];
        for (int k = 0; k <= n; k++)
        {
            float t = Mathf.Min(k * angleStep, angle);
            samples[k] = a * Mathf.Cos(t) + perp * Mathf.Sin(t);
        }
        return new Link { name = linkName, samples = samples };
    }

    private void TryCandidate(Quaternion q, List<Link> links, ref Quaternion best, ref int bestBlocked, ref float bestMargin)
    {
        if (!Evaluate(q, links, bestBlocked, out int blocked, out float margin)) return;
        if (blocked < bestBlocked || (blocked == bestBlocked && margin < bestMargin))
        {
            best = q; bestBlocked = blocked; bestMargin = margin;
        }
    }

    private bool Evaluate(Quaternion rot, List<Link> links, int maxBlocked, out int blocked, out float margin)
    {
        blocked = 0; margin = 0f;
        foreach (Link link in links)
        {
            float worst = TraceLink(rot, link);
            if (worst > BlockLimit && ++blocked > maxBlocked) return false;
            margin += Mathf.Min(worst, 5f);
        }
        return true;
    }

    // Pire rapport |variation du sol| / variation autorisée par pas (> 1 = coupé). 99 = trop long ou pas de sol.
    private float TraceLink(Quaternion rot, Link link)
    {
        Vector3 d0 = rot * link.samples[0];
        float prev = SampleGrid(d0);
        if (prev < 0f) return 99f;
        Vector3 prevPoint = d0 * (prev + meanHeight);
        float worst = 0f, length = 0f;

        for (int k = 1; k < link.samples.Length; k++)
        {
            Vector3 d = rot * link.samples[k];
            float r = SampleGrid(d);
            if (r < 0f) return 99f;
            worst = Mathf.Max(worst, Mathf.Abs(r - prev) / maxRise);
            Vector3 point = d * (r + meanHeight);
            length += (point - prevPoint).magnitude;
            prevPoint = point;
            prev = r;
        }
        return length > maxDistance ? 99f : worst;
    }

    private void BuildGrid()
    {
        latCount = Mathf.Max(19, Mathf.RoundToInt(180f / gridResolution) + 1);
        lonCount = Mathf.Max(36, Mathf.RoundToInt(360f / gridResolution));
        grid = new float[latCount, lonCount];

        double sum = 0; int hits = 0, misses = 0;
        for (int i = 0; i < latCount; i++)
        {
            float theta = Mathf.PI * i / (latCount - 1);
            for (int j = 0; j < lonCount; j++)
            {
                float phi = 2f * Mathf.PI * j / lonCount;
                Vector3 dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                if (Ground(dir, out float r)) { grid[i, j] = r; sum += r; hits++; }
                else { grid[i, j] = -1f; misses++; }
            }
        }
        if (misses > 0) Debug.LogWarning($"{name} : {misses} sondes n'ont pas touché le sol (comptées comme bloquantes).");
        meanGround = hits > 0 ? (float)(sum / hits) : probeRadius;
    }

    // Hauteur moyenne des départs de faisceau au-dessus du sol
    private void MeasureHeight()
    {
        float sum = 0f; int n = 0;
        foreach (Crystal c in GetComponentsInChildren<Crystal>(true))
        {
            Vector3 s = c.BeamStart() - center;
            if (Ground(s.normalized, out float g)) { sum += Mathf.Clamp(s.magnitude - g, 0f, 3f); n++; }
        }
        meanHeight = n > 0 ? sum / n : 0.5f;
    }

    private float SampleGrid(Vector3 d)
    {
        float theta = Mathf.Acos(Mathf.Clamp(d.y, -1f, 1f));
        float phi = Mathf.Atan2(d.z, d.x);
        if (phi < 0f) phi += 2f * Mathf.PI;

        float fi = theta / Mathf.PI * (latCount - 1);
        float fj = phi / (2f * Mathf.PI) * lonCount;
        int i0 = Mathf.Min((int)fi, latCount - 2);
        int jf = (int)fj;
        int j0 = jf % lonCount;
        int j1 = (j0 + 1) % lonCount;
        float ti = fi - i0, tj = fj - jf;

        float a = grid[i0, j0], b = grid[i0, j1], c = grid[i0 + 1, j0], e = grid[i0 + 1, j1];
        if (a < 0f || b < 0f || c < 0f || e < 0f) return -1f;
        return Mathf.Lerp(Mathf.Lerp(a, b, tj), Mathf.Lerp(c, e, tj), ti);
    }

    private static Vector3 Fibonacci(int i, int n)
    {
        float y = 1f - 2f * (i + 0.5f) / n;
        float r = Mathf.Sqrt(1f - y * y);
        float phi = i * 2.39996323f;
        return new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r);
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.up * 30f);
        if (crystalToMove != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, crystalToMove.transform.position);
        }
    }
}