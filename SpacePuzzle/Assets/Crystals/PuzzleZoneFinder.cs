using UnityEngine;

// Outil éditeur : oriente le conteneur du puzzle (+Y local) vers la zone la plus plate de la planète.
// À placer sur le même objet que le BeamManager (Crystal_Puzzle), centré sur la planète.
[RequireComponent(typeof(BeamManager))]
public class PuzzleZoneFinder : MonoBehaviour
{
    [Header("Zone recherchée")]
    [SerializeField, Range(10f, 90f)] private float coreAngle = 45f;   // rayon du cœur du puzzle autour du pôle (degrés)
    [SerializeField] private float maxSlopeAngle = 35f;                // même valeur que le BeamManager
    [SerializeField] private float probeStep = 0.25f;                  // même valeur que Step Length

    [Header("Précision")]
    [SerializeField] private int sampleCount = 30000;                  // points de mesure sur toute la planète
    [SerializeField] private int candidateCount = 1500;                // pôles testés
    [SerializeField] private LayerMask groundMask;                     // Planete (vide = Planete)

    [ContextMenu("Find flattest zone")]
    private void FindFlattestZone()
    {
        BeamManager manager = GetComponent<BeamManager>();
        Transform planet = manager.PlanetTransform;
        if (planet == null) { Debug.LogWarning($"{name} : le BeamManager n'a pas de planète assignée."); return; }

        Vector3 center = planet.position;
        if ((transform.position - center).sqrMagnitude > 0.0001f)
        {
            Debug.LogWarning($"{name} n'est pas au centre de la planète : mets sa Position locale à (0, 0, 0).");
            return;
        }

        int mask = groundMask.value != 0 ? groundMask.value : LayerMask.GetMask("Planete");
        Physics.SyncTransforms();

        // Rayon de départ des sondes : au-dessus du plus haut relief
        float startRadius = 1f;
        foreach (Collider c in planet.GetComponentsInChildren<Collider>())
            if ((mask & (1 << c.gameObject.layer)) != 0)
                startRadius = Mathf.Max(startRadius, Vector3.Distance(c.bounds.center, center) + c.bounds.extents.magnitude);
        startRadius += 1f;

        var watch = System.Diagnostics.Stopwatch.StartNew();

        // 1. Pente du terrain en chaque point de mesure
        Vector3[] dirs = new Vector3[sampleCount];
        float[] slopes = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            dirs[i] = Fibonacci(i, sampleCount);
            slopes[i] = SlopeAt(planet, dirs[i], startRadius, mask);
        }

        // 2. Pôle dont le cône contient le moins de points trop raides (puis la pente moyenne la plus faible)
        float cosCore = Mathf.Cos(coreAngle * Mathf.Deg2Rad);
        int bestSteep = int.MaxValue, bestCount = 0;
        float bestMean = float.MaxValue, bestMax = 0f;
        Vector3 bestDir = transform.up;

        for (int c = 0; c < candidateCount; c++)
        {
            Vector3 pole = Fibonacci(c, candidateCount);
            int steep = 0, count = 0;
            float sum = 0f, max = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                if (Vector3.Dot(pole, dirs[i]) < cosCore) continue;
                float s = slopes[i];
                count++;
                sum += s;
                if (s > max) max = s;
                if (s > maxSlopeAngle) steep++;
            }
            if (count == 0) continue;

            float mean = sum / count;
            if (steep < bestSteep || (steep == bestSteep && mean < bestMean))
            {
                bestSteep = steep; bestMean = mean; bestMax = max; bestCount = count; bestDir = pole;
            }
        }

        // 3. Orienter le conteneur : son +Y pointe vers la zone trouvée (rotation minimale)
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(transform, "Find flattest zone");
#endif
        transform.rotation = Quaternion.FromToRotation(transform.up, bestDir) * transform.rotation;

        Debug.Log($"{name} : zone trouvée en {watch.ElapsedMilliseconds} ms. " +
                  $"Points trop raides : {bestSteep}/{bestCount} ({100f * bestSteep / bestCount:F1} %), " +
                  $"pente moyenne {bestMean:F1}°, pente max {bestMax:F1}°.");
    }

    // Pente locale (degrés) : norme du gradient de hauteur, mesurée sur probeStep comme le faisceau
    private float SlopeAt(Transform planet, Vector3 dir, float startRadius, int mask)
    {
        if (!TerrainSnap.GroundRadius(planet, dir, startRadius, mask, out float r0, out _)) return 90f;

        Vector3 t1 = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 t2 = Vector3.Cross(dir, t1);
        float a = probeStep / r0;   // angle (radians) pour avancer de probeStep au sol

        if (!TerrainSnap.GroundRadius(planet, (dir + t1 * a).normalized, startRadius, mask, out float r1, out _)) return 90f;
        if (!TerrainSnap.GroundRadius(planet, (dir + t2 * a).normalized, startRadius, mask, out float r2, out _)) return 90f;

        float rise = Mathf.Sqrt((r1 - r0) * (r1 - r0) + (r2 - r0) * (r2 - r0));
        return Mathf.Atan2(rise, probeStep) * Mathf.Rad2Deg;
    }

    // Directions réparties uniformément sur la sphère (spirale de Fibonacci)
    private static Vector3 Fibonacci(int i, int n)
    {
        float y = 1f - 2f * (i + 0.5f) / n;
        float r = Mathf.Sqrt(1f - y * y);
        float phi = i * 2.39996323f;   // angle d'or
        return new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.up * 30f);   // pôle du puzzle
    }
}
