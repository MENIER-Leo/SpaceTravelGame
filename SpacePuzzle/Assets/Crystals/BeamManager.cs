using System.Collections.Generic;
using UnityEngine;


[DefaultExecutionOrder(100)]
public class BeamManager : MonoBehaviour
{

    [Header("Réglages")]
    [SerializeField] 
    private LayerMask beamMask;
    [SerializeField] 
    private float maxDistance = 50f;
    [SerializeField] 
    private int maxIterations = 10;
    [SerializeField] private bool debugLogs = false;

    [SerializeField] 
    private Transform planet;
    [SerializeField] 
    private float stepLength = 0.4f;

    [Header("Terrain")]
    [SerializeField] private LayerMask groundMask;          // Planete
    [SerializeField] private float maxSlopeAngle = 35f;     // au-delà, le faisceau est coupé
    [SerializeField] private bool snapOnStart = true;       // recale les cristaux au lancement

    private struct BeamSegment
    {
        public Vector3[] points;
        public Colors color;
    }

    private Crystal[] crystals;
    private readonly List<BeamSegment> segments = new List<BeamSegment>();

    [Header("Affichage")]
    [SerializeField] private LineRenderer segmentPrefab;
    [SerializeField] private float emissionIntensity = 4f;

    private readonly List<LineRenderer> pool = new List<LineRenderer>();
    private MaterialPropertyBlock propertyBlock;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public Transform PlanetTransform => planet;

    public event System.Action OnRecomputed;


    private void Start()
    {
        RefreshCrystals();
        if (snapOnStart) SnapAllToTerrain();
        Recompute();
    }


    private void OnEnable()
    {
        Crystal.OnAnyCrystalChanged += Recompute;
    }

    private void OnDisable()
    {
        Crystal.OnAnyCrystalChanged -= Recompute;
        HideAllSegments();
    }

    [ContextMenu("Refresh crystals")]
    private void RefreshCrystals()
    {
        crystals = GetComponentsInChildren<Crystal>();
    }

    [ContextMenu("Snap all to terrain")]
    private void SnapAllToTerrain()
    {
        foreach (Crystal c in GetComponentsInChildren<Crystal>()) c.AlignToPlanet(false);
        foreach (ResetCrystal r in GetComponentsInChildren<ResetCrystal>()) r.AlignToPlanet();
    }

    // Vrai si tous les récepteurs de la scène sont satisfaits
    public bool AllReceiversSatisfied()
    {
        if (crystals == null) RefreshCrystals();

        bool anyReceiver = false;

        foreach (Crystal c in crystals)
        {
            if (c.crystalType != CrystalType.Receiver) continue;
            anyReceiver = true;
            if (!c.IsSatisfied) return false;
        }

        return anyReceiver;
    }

    [ContextMenu("Recompute")]
    public void Recompute()
    {
        if (crystals == null) RefreshCrystals();

        int count = crystals.Length;

        // Départ : tout à zéro, seuls les émetteurs ont une sortie
        foreach (Crystal c in crystals) c.ResetInput();

        Colors[] previous = new Colors[count];
        for (int i = 0; i < count; i++) previous[i] = crystals[i].OutputColor;
        bool stable = false;

        // On répète les passes jusqu'à ce que plus rien ne change
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            segments.Clear();
            foreach (Crystal c in crystals) c.ResetInput();

            // Chaque cristal qui émet tire un faisceau
            for (int i = 0; i < count; i++)
            {
                Crystal source = crystals[i];
                Colors color = previous[i];

                if (color == Colors.None) continue;
                if (source.crystalType == CrystalType.Receiver) continue;

                TraceBeam(source, color);
            }

            // Comparaison avec la passe précédente
            stable = true;
            for (int i = 0; i < count; i++)
            {
                Colors current = crystals[i].OutputColor;
                if (current != previous[i]) stable = false;
                previous[i] = current;
            }

            if (stable) break;
        }


        if (!stable) Debug.LogWarning($"{name} : faisceaux instables (boucle de mixeur ?).");
        DrawBeams();
        foreach (Crystal c in crystals) c.UpdateVisual();
        if (debugLogs) LogReceivers();
        OnRecomputed?.Invoke();
    }

    private void TraceBeam(Crystal source, Colors color)
    {
        if (planet == null)
        {
            Debug.LogWarning($"{name} : aucune planète assignée.");
            return;
        }

        Vector3 center = planet.position;
        Vector3 start = source.BeamStart();
        Vector3 dir = (start - center).normalized;
        Vector3 heading = Vector3.ProjectOnPlane(source.Heading, dir).normalized;

        if (heading == Vector3.zero)
        {
            Debug.LogWarning($"{source.name} : Heading perpendiculaire à la surface, cristal mal aligné.");
            return;
        }

        int ground = groundMask.value != 0 ? groundMask.value : LayerMask.GetMask("Planete");
        int hitMask = beamMask & ~ground;                       // le long du faisceau : uniquement les cristaux
        float startRadius = (start - center).magnitude;
        float probeStart = startRadius + 20f;

        // Hauteur du faisceau au-dessus du sol, prise au départ
        if (!TerrainSnap.GroundRadius(planet, dir, probeStart, ground, out float groundR, out _))
        {
            Debug.LogWarning($"{source.name} : sol introuvable sous le cristal.");
            return;
        }
        float height = startRadius - groundR;

        Vector3 axis = Vector3.Cross(dir, heading).normalized;
        Quaternion stepRotation = Quaternion.AngleAxis(stepLength / startRadius * Mathf.Rad2Deg, axis);
        float maxRise = Mathf.Tan(maxSlopeAngle * Mathf.Deg2Rad) * stepLength;

        List<Vector3> points = new List<Vector3> { start };
        Vector3 current = start;
        float travelled = 0f;

        while (travelled < maxDistance)
        {
            Vector3 nextDir = stepRotation * dir;

            // Pas de sol (trou) : faisceau coupé
            if (!TerrainSnap.GroundRadius(planet, nextDir, probeStart, ground, out float nextGroundR, out _))
                break;

            // Pente trop forte : faisceau coupé
            if (Mathf.Abs(nextGroundR - groundR) > maxRise)
            {
                if (nextGroundR > groundR)   // montée : il s'écrase sur la paroi
                {
                    Vector3 flat = center + nextDir * (current - center).magnitude;
                    Vector3 d = flat - current;
                    if (Physics.Raycast(current, d.normalized, out RaycastHit wall, d.magnitude,
                                        ground, QueryTriggerInteraction.Ignore))
                        points.Add(wall.point);
                    else
                        points.Add(flat);
                }
                break;
            }

            Vector3 next = center + nextDir * (nextGroundR + height);
            Vector3 chord = next - current;
            float length = chord.magnitude;

            if (Physics.Raycast(current, chord / length, out RaycastHit hit, length,
                                hitMask, QueryTriggerInteraction.Ignore))
            {
                points.Add(hit.point);

                Crystal target = hit.collider.GetComponentInParent<Crystal>();
                if (target != null && target != source) target.AddInput(color);
                break;
            }

            points.Add(next);
            current = next;
            dir = nextDir;
            groundR = nextGroundR;
            travelled += length;
        }

        segments.Add(new BeamSegment { points = points.ToArray(), color = color });
    }

    private LineRenderer GetSegment()
    {
        foreach (LineRenderer lr in pool)
            if (!lr.gameObject.activeSelf) return lr;

        LineRenderer created = Instantiate(segmentPrefab, transform);
        pool.Add(created);
        return created;
    }

    private void HideAllSegments()
    {
        foreach (LineRenderer lr in pool)
            if (lr != null) lr.gameObject.SetActive(false);
    }

    private void DrawBeams()
    {
        if (!Application.isPlaying || segmentPrefab == null) return;
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        HideAllSegments();

        foreach (BeamSegment s in segments)
        {
            LineRenderer lr = GetSegment();
            lr.positionCount = s.points.Length;
            lr.SetPositions(s.points);

            propertyBlock.SetColor(EmissionColorId, ColorsUtil.ToColor(s.color) * emissionIntensity);
            lr.SetPropertyBlock(propertyBlock);

            lr.gameObject.SetActive(true);
        }
    }

    private void OnDrawGizmos()
    {
        foreach (BeamSegment s in segments)
        {
            Gizmos.color = ColorsUtil.ToColor(s.color);
            for (int i = 0; i < s.points.Length - 1; i++)
                Gizmos.DrawLine(s.points[i], s.points[i + 1]);
        }
    }

    private void LogReceivers()
    {
        foreach (Crystal c in crystals)
        {
            if (c.crystalType != CrystalType.Receiver) continue;
            Debug.Log($"{c.name} : reçoit {c.ReceivedColor}, cible {c.targetColor}, satisfait : {c.IsSatisfied}");
        }
    }
}