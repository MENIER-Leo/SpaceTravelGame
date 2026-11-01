using System.Collections.Generic;
using UnityEngine;

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

    // Vrai si tous les récepteurs de la scène sont satisfaits
    public bool AllReceiversSatisfied()
    {
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
        Vector3 center = planet != null ? planet.position : Vector3.zero;
        Vector3 current = source.BeamStart();
        Vector3 normal = (current - center).normalized;
        Vector3 direction = Vector3.ProjectOnPlane(source.Heading, normal).normalized;

        if (direction == Vector3.zero)
        {
            Debug.LogWarning($"{source.name} : Heading perpendiculaire à la surface, cristal mal aligné.");
            return;
        }

        float altitude = (current - center).magnitude;
        Vector3 axis = Vector3.Cross(normal, direction).normalized;
        float angle = stepLength / altitude * Mathf.Rad2Deg;
        Quaternion stepRotation = Quaternion.AngleAxis(angle, axis);

        List<Vector3> points = new List<Vector3> { current };
        float travelled = 0f;

        while (travelled < maxDistance)
        {
            Vector3 next = center + stepRotation * (current - center);
            Vector3 chord = next - current;
            float length = chord.magnitude;

            if (Physics.Raycast(current, chord / length, out RaycastHit hit, length,
                                beamMask, QueryTriggerInteraction.Ignore))
            {
                points.Add(hit.point);

                Crystal target = hit.collider.GetComponentInParent<Crystal>();
                if (target != null && target != source) target.AddInput(color);
                break;
            }

            points.Add(next);
            current = next;
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