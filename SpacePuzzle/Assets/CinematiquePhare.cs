using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Fait apparaître les émetteurs du Phare quand leur région s'allume.
// Plan rapproché sur chaque cristal qui sort du sol (le Phare en arrière-plan),
// puis, après le dernier, plan large sur les trois et plan sur le foyer.
// Nécessite l'événement onRegionLit dans RegionLight.
public class CinematiquePhare : MonoBehaviour
{
    [System.Serializable]
    public class Eveil
    {
        [Tooltip("Propriété de la région dans RegionLight : _Lit_Rouge, _Lit_Vert, _Lit_Bleu.")]
        public string regionProperty;
        public Crystal crystal;
    }

    [SerializeField] private RegionLight regionLight;
    [SerializeField] private Eveil[] eveils;

    [Header("Caméra")]
    [Tooltip("Le Phare : il reste en arrière-plan des plans rapprochés.")]
    [SerializeField] private Transform phare;
    [Tooltip("Plan sur le foyer après le dernier cristal (vide = pas de plan).")]
    [SerializeField] private Transform lensViewPoint;
    [Tooltip("Sol de la planète : la caméra ne descend jamais dessous.")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float flyTime = 1.5f;
    [Tooltip("Hauteur supplémentaire au milieu du vol, pour passer au-dessus du relief.")]
    [SerializeField] private float flyLift = 6f;
    [Tooltip("Plan rapproché : distance de la caméra au cristal.")]
    [SerializeField] private float closeDistance = 6f;
    [Tooltip("Plan rapproché : hauteur de la caméra au-dessus du cristal.")]
    [SerializeField] private float closeHeight = 2.5f;
    [SerializeField] private float holdTime = 1.2f;
    [Tooltip("Durée du plan large sur les trois cristaux.")]
    [SerializeField] private float groupHoldTime = 2.5f;
    [SerializeField] private float lensHoldTime = 1.5f;

    [Header("Apparition")]
    [SerializeField] private float riseTime = 1.2f;
    [SerializeField] private float riseDepth = 2.5f;
    [SerializeField] private float spinTurns = 1f;
    [SerializeField] private float flashIntensity = 15f;
    [SerializeField] private float flashRange = 12f;

    [Header("Scripts coupés pendant la cinématique")]
    [SerializeField] private MonoBehaviour[] disableDuring;

    private readonly Queue<Eveil> queue = new Queue<Eveil>();
    private readonly HashSet<Crystal> revealed = new HashSet<Crystal>();
    private bool playing;
    private bool groupShown;
    private BeamManager manager;

    public bool IsPlaying => playing;

    private bool HasPlanet => manager != null && manager.PlanetTransform != null;
    private Vector3 Center => HasPlanet ? manager.PlanetTransform.position : Vector3.zero;
    private Vector3 Up(Vector3 p) => HasPlanet ? (p - Center).normalized : Vector3.up;

    private void Awake()
    {
        manager = GetComponentInParent<BeamManager>();
        if (manager == null) manager = FindFirstObjectByType<BeamManager>();
    }

    private void OnEnable()
    {
        if (regionLight != null) regionLight.onRegionLit.AddListener(OnRegionLit);
    }

    private void OnDisable()
    {
        if (regionLight != null) regionLight.onRegionLit.RemoveListener(OnRegionLit);
    }

    // Avant le Start du BeamManager (ordre 100) : les émetteurs cachés ne sont pas dans sa liste.
    private void Start()
    {
        if (regionLight == null) Debug.LogWarning($"{name} : aucun RegionLight assigné, les cristaux du Phare n'apparaîtront jamais.");
        foreach (Eveil e in eveils)
            if (e.crystal != null) e.crystal.gameObject.SetActive(false);
    }

    private void OnRegionLit(string property)
    {
        foreach (Eveil e in eveils)
        {
            if (e.crystal == null || e.regionProperty != property || revealed.Contains(e.crystal)) continue;
            revealed.Add(e.crystal);
            queue.Enqueue(e);
        }
        if (!playing && queue.Count > 0) StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        playing = true;
        bool[] states = SetScripts(null);

        Camera cam = Camera.main;
        Transform ct = cam != null ? cam.transform : null;
        Vector3 p0 = ct != null ? ct.position : Vector3.zero;
        Quaternion r0 = ct != null ? ct.rotation : Quaternion.identity;

        // Un plan rapproché par cristal
        while (queue.Count > 0)
        {
            Eveil next = queue.Dequeue();
            if (ct != null)
            {
                CloseShot(next.crystal.transform.position, out Vector3 cPos, out Quaternion cRot);
                yield return Fly(ct, cPos, cRot);
            }
            yield return Reveal(next);
            yield return new WaitForSeconds(holdTime);
        }

        // Les trois sont là : plan large, puis le foyer
        if (!groupShown && AllRevealed())
        {
            groupShown = true;
            if (ct != null && GroupShot(cam, out Vector3 gPos, out Quaternion gRot))
            {
                yield return Fly(ct, gPos, gRot);
                foreach (Eveil e in eveils)
                    if (e.crystal != null) e.crystal.Flash(3f, 0.8f);
                yield return new WaitForSeconds(groupHoldTime);
            }
            if (ct != null && lensViewPoint != null)
            {
                yield return Fly(ct, lensViewPoint.position, lensViewPoint.rotation);
                yield return new WaitForSeconds(lensHoldTime);
            }
        }

        if (ct != null) yield return Fly(ct, p0, r0);

        SetScripts(states);
        playing = false;
        if (queue.Count > 0) StartCoroutine(Play());
    }

    private bool AllRevealed()
    {
        foreach (Eveil e in eveils)
            if (e.crystal != null && !revealed.Contains(e.crystal)) return false;
        return true;
    }

    // ---------- Cadrages ----------

    // Direction qui s'éloigne du Phare : la caméra se place de ce côté et regarde vers le Phare.
    private Vector3 Away(Vector3 p, Vector3 up)
    {
        Vector3 from = phare != null ? phare.position : transform.position;
        Vector3 a = Vector3.ProjectOnPlane(p - from, up);
        if (a.sqrMagnitude < 0.01f) a = Vector3.ProjectOnPlane(-transform.forward, up);
        return a.normalized;
    }

    private void CloseShot(Vector3 p, out Vector3 pos, out Quaternion rot)
    {
        Vector3 up = Up(p);
        pos = KeepAboveGround(p + Away(p, up) * closeDistance + up * closeHeight, 1.5f);
        rot = Quaternion.LookRotation(p + up * 0.8f - pos, Up(pos));
    }

    private bool GroupShot(Camera cam, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;
        List<Vector3> pts = new List<Vector3>();
        foreach (Eveil e in eveils)
            if (e.crystal != null) pts.Add(e.crystal.transform.position);
        if (pts.Count == 0) return false;

        Vector3 c = Vector3.zero;
        foreach (Vector3 p in pts) c += p;
        c /= pts.Count;
        float radius = 3f;
        foreach (Vector3 p in pts) radius = Mathf.Max(radius, Vector3.Distance(p, c));

        // Distance pour que tous les cristaux tiennent dans le champ de la caméra
        float fov = Mathf.Min(cam.fieldOfView, Camera.VerticalToHorizontalFieldOfView(cam.fieldOfView, cam.aspect));
        float d = (radius + 2f) / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad);

        Vector3 up = Up(c);
        Vector3 dir = (Away(c, up) * 0.7f + up * 0.7f).normalized;
        pos = KeepAboveGround(c + dir * d, 2f);
        rot = Quaternion.LookRotation(c + up * 0.5f - pos, Up(pos));
        return true;
    }

    private Vector3 KeepAboveGround(Vector3 pos, float clearance)
    {
        if (!HasPlanet || groundMask.value == 0) return pos;
        Vector3 up = Up(pos);
        float r = (pos - Center).magnitude;
        if (Physics.Raycast(Center + up * (r + 100f), -up, out RaycastHit hit, r + 200f, groundMask))
        {
            float g = (hit.point - Center).magnitude + clearance;
            if (r < g) return Center + up * g;
        }
        return pos;
    }

    // ---------- Animation ----------

    // restore == null : coupe les scripts et renvoie leur état ; sinon : le restaure.
    private bool[] SetScripts(bool[] restore)
    {
        if (disableDuring == null) return null;
        if (restore == null)
        {
            bool[] s = new bool[disableDuring.Length];
            for (int i = 0; i < disableDuring.Length; i++)
            {
                if (disableDuring[i] == null) continue;
                s[i] = disableDuring[i].enabled;
                disableDuring[i].enabled = false;
            }
            return s;
        }
        for (int i = 0; i < disableDuring.Length; i++)
            if (disableDuring[i] != null) disableDuring[i].enabled = restore[i];
        return null;
    }

    // Vol en arc autour de la planète (ne traverse pas le sol).
    private IEnumerator Fly(Transform t, Vector3 pos, Quaternion rot)
    {
        Vector3 p = t.position;
        Quaternion r = t.rotation;
        Vector3 c = Center;

        for (float x = 0f; x < flyTime; x += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, x / flyTime);
            Vector3 at;
            if (HasPlanet)
            {
                Vector3 a = p - c, b = pos - c;
                float radius = Mathf.Lerp(a.magnitude, b.magnitude, k) + Mathf.Sin(k * Mathf.PI) * flyLift;
                at = c + Vector3.Slerp(a.normalized, b.normalized, k) * radius;
            }
            else at = Vector3.Lerp(p, pos, k);

            t.SetPositionAndRotation(at, Quaternion.Slerp(r, rot, k));
            yield return null;
        }
        t.SetPositionAndRotation(pos, rot);
    }

    private IEnumerator Reveal(Eveil e)
    {
        Crystal c = e.crystal;
        Transform t = c.transform;
        Vector3 end = t.position;
        Quaternion endRot = t.rotation;
        Vector3 up = Up(end);
        Vector3 start = end - up * riseDepth;

        t.SetPositionAndRotation(start, endRot);
        c.gameObject.SetActive(true);

        Light flash = new GameObject("Flash_" + c.name).AddComponent<Light>();
        flash.transform.position = end + up * 1.5f;
        flash.type = LightType.Point;
        flash.range = flashRange;
        flash.color = ColorsUtil.ToColor(c.emittedColor);
        flash.intensity = 0f;

        // Montée en tournant
        for (float x = 0f; x < riseTime; x += Time.deltaTime)
        {
            float k = x / riseTime;
            float s = Mathf.SmoothStep(0f, 1f, k);
            t.SetPositionAndRotation(Vector3.Lerp(start, end, s),
                                     Quaternion.AngleAxis((1f - s) * spinTurns * 360f, up) * endRot);
            flash.intensity = flashIntensity * 0.4f * k;
            yield return null;
        }
        t.SetPositionAndRotation(end, endRot);

        // Le cristal entre dans le calcul des rayons et s'allume
        Physics.SyncTransforms();
        RefreshBeams();
        c.Flash(4f, 1f);

        // Éclair puis extinction
        const float burst = 0.8f;
        for (float x = 0f; x < burst; x += Time.deltaTime)
        {
            flash.intensity = flashIntensity * (1f - x / burst);
            yield return null;
        }
        Destroy(flash.gameObject);
    }

    private void RefreshBeams()
    {
        if (manager != null)
            typeof(BeamManager).GetMethod("RefreshCrystals", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(manager, null);
        Crystal.NotifyChanged();
    }
}