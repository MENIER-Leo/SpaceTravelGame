using UnityEngine;
using UnityEngine.Events;

// Récepteur à charger : doit rester satisfait chargeTime s au total ; la charge redescend dans le noir.
// Une couronne d'éclats autour du récepteur sert de jauge (créée au lancement).
[RequireComponent(typeof(Crystal))]
public class Charge : MonoBehaviour
{
    [SerializeField] private float chargeTime = 10f;
    [Tooltip("Charge perdue par seconde quand il n'est pas satisfait.")]
    [SerializeField] private float decayRate = 1f;
    [Tooltip("Relais qui arrêtent de surchauffer une fois chargé.")]
    [SerializeField] private Surchauffe[] stabilizeOnCharged;

    [Header("Couronne de fragments (créée au lancement)")]
    [SerializeField] private int fragmentCount = 8;
    [SerializeField] private float crownRadius = 1.6f;
    [SerializeField] private Vector3 fragmentSize = new Vector3(0.22f, 0.8f, 0.22f);
    [Tooltip("Inclinaison des éclats vers l'extérieur (degrés).")]
    [SerializeField] private float fragmentTilt = 20f;
    [Tooltip("Vide = matériau du récepteur. Doit avoir Emission coché pour briller.")]
    [SerializeField] private Material fragmentMaterial;
    [SerializeField] private Color litColor = new Color(1f, 0.2f, 1f);
    [SerializeField] private Color offColor = new Color(0.12f, 0.08f, 0.12f);
    [SerializeField] private float emission = 4f;

    [Header("Lumière (optionnel)")]
    [SerializeField] private Light chargeLight;
    [SerializeField] private float maxIntensity = 8f;

    public UnityEvent onCharged;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private Crystal crystal;
    private float charge;
    private bool charged;
    private Renderer[] fragments;
    private MaterialPropertyBlock mpb;

    public float Charge01 => chargeTime > 0f ? charge / chargeTime : 1f;
    public bool IsCharged => charged;

    private void Awake()
    {
        crystal = GetComponent<Crystal>();
        mpb = new MaterialPropertyBlock();
        BuildCrown();
    }

    private void Update()
    {
        if (charged) return;
        float dt = Time.deltaTime;
        charge += crystal.IsSatisfied ? dt : -decayRate * dt;
        charge = Mathf.Clamp(charge, 0f, chargeTime);
        if (charge >= chargeTime) Complete();
    }

    private void LateUpdate()
    {
        UpdateCrown();
        if (chargeLight != null) chargeLight.intensity = maxIntensity * Charge01;
    }

    private void Complete()
    {
        charged = true;
        if (stabilizeOnCharged != null)
            foreach (Surchauffe s in stabilizeOnCharged) if (s != null) s.Stabilize();
        onCharged?.Invoke();
    }

    // Pour un futur Restart
    public void ResetCharge()
    {
        charged = false;
        charge = 0f;
    }

    // ---------- Couronne ----------

    private void BuildCrown()
    {
        if (fragmentCount <= 0) return;

        BeamManager m = GetComponentInParent<BeamManager>();
        Vector3 c = m != null && m.PlanetTransform != null ? m.PlanetTransform.position : transform.position - transform.up * 10f;
        Vector3 up = (transform.position - c).normalized;
        Vector3 tangent = Vector3.ProjectOnPlane(transform.forward, up);
        if (tangent.sqrMagnitude < 1e-4f) tangent = Vector3.ProjectOnPlane(transform.right, up);
        tangent.Normalize();
        int mask = LayerMask.GetMask("Planete");

        Material mat = fragmentMaterial;
        if (mat == null)
        {
            Renderer rr = GetComponentInChildren<Renderer>();
            if (rr != null) mat = rr.sharedMaterial;
        }

        GameObject crown = new GameObject("Couronne_Charge");
        crown.transform.SetParent(transform.parent, true);

        fragments = new Renderer[fragmentCount];
        for (int i = 0; i < fragmentCount; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(360f * i / fragmentCount, up) * tangent;
            Vector3 p = transform.position + dir * crownRadius;
            Vector3 normal = up;

            Vector3 radial = (p - c).normalized;
            float r = (p - c).magnitude;
            if (Physics.Raycast(c + radial * (r + 20f), -radial, out RaycastHit hit, r + 40f, mask))
            {
                p = hit.point;
                normal = hit.normal;
            }

            GameObject f = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(f.GetComponent<Collider>());   // ne gêne ni les rayons ni le joueur
            f.name = $"Eclat_{i + 1}";
            f.transform.SetParent(crown.transform, true);

            Vector3 outDir = Vector3.ProjectOnPlane(dir, normal).normalized;
            Quaternion rot = Quaternion.AngleAxis(fragmentTilt, Vector3.Cross(normal, outDir)) * Quaternion.LookRotation(outDir, normal);
            f.transform.rotation = rot;
            f.transform.localScale = fragmentSize;
            f.transform.position = p + rot * Vector3.up * (fragmentSize.y * 0.35f);

            Renderer rend = f.GetComponent<Renderer>();
            if (mat != null) rend.sharedMaterial = mat;
            fragments[i] = rend;
        }
    }

    private void UpdateCrown()
    {
        if (fragments == null) return;
        int n = fragments.Length;
        float level = Charge01 * n;
        bool draining = !charged && charge > 0f && !crystal.IsSatisfied;
        int top = Mathf.Clamp(Mathf.CeilToInt(level) - 1, 0, n - 1);

        for (int i = 0; i < n; i++)
        {
            float f = charged ? 1f : Mathf.Clamp01(level - i);
            float e = emission * f;
            if (charged) e = emission * (1.2f + 0.4f * Mathf.Sin(Time.time * 3f + i * 0.8f));
            else if (draining && i == top && f > 0f) e *= 0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(Time.time * 12f));

            Color col = Color.Lerp(offColor, litColor, f);
            fragments[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, col);
            mpb.SetColor(ColorId, col);
            mpb.SetColor(EmissionId, litColor * e);
            fragments[i].SetPropertyBlock(mpb);
        }
    }
}