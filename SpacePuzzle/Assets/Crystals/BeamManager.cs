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

    private struct BeamSegment
    {
        public Vector3 start;
        public Vector3 end;
        public Colors color;
    }

    private Crystal[] crystals;
    private readonly List<BeamSegment> segments = new List<BeamSegment>();


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
    }

    [ContextMenu("Refresh crystals")]
    private void RefreshCrystals()
    {
        crystals = FindObjectsByType<Crystal>(FindObjectsSortMode.None);
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
            bool stable = true;
            for (int i = 0; i < count; i++)
            {
                Colors current = crystals[i].OutputColor;
                if (current != previous[i]) stable = false;
                previous[i] = current;
            }

            if (stable) break;
        }

        LogReceivers();
    }

    private void TraceBeam(Crystal source, Colors color)
    {
        Vector3 origin = source.BeamStart();
        Vector3 direction = source.Heading;
        Vector3 end = origin + direction * maxDistance;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance,
                            beamMask, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;

            Crystal target = hit.collider.GetComponentInParent<Crystal>();
            if (target != null && target != source)
            {
                target.AddInput(color);
            }
        }

        segments.Add(new BeamSegment { start = origin, end = end, color = color });
    }

    // Convertit une couleur de l'enum en couleur Unity
    private static Color ToColor(Colors c)
    {
        if (c == Colors.None) return new Color(0.3f, 0.3f, 0.3f);

        float r = (c & Colors.Red) != 0 ? 1f : 0f;
        float g = (c & Colors.Green) != 0 ? 1f : 0f;
        float b = (c & Colors.Blue) != 0 ? 1f : 0f;
        return new Color(r, g, b);
    }

    private void OnDrawGizmos()
    {
        foreach (BeamSegment s in segments)
        {
            Gizmos.color = ToColor(s.color);
            Gizmos.DrawLine(s.start, s.end);
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