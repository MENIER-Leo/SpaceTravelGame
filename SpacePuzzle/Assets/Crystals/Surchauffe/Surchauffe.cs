using UnityEngine;

// Relais qui surchauffe : après heatTime s à relayer, il s'éteint jusqu'à avoir complètement refroidi.
// Lisibilité : le cristal vire orange → rouge, tremble dans la dernière seconde, puis rougeoie comme une braise.
[RequireComponent(typeof(Crystal))]
public class Surchauffe : MonoBehaviour
{
    [Tooltip("Secondes à relayer avant la surchauffe.")]
    [SerializeField] private float heatTime = 4f;
    [Tooltip("Vitesse de refroidissement (1 = aussi vite qu'il chauffe).")]
    [SerializeField] private float coolRate = 1f;

    [Header("Lisibilité")]
    [Tooltip("Vide = premier enfant qui a un Renderer.")]
    [SerializeField] private Transform visual;
    [SerializeField] private float shiverTime = 1.2f;
    [SerializeField] private float shiverAngle = 6f;
    [Tooltip("Part de la chaleur (0-1) à partir de laquelle le cristal change de couleur.")]
    [SerializeField] private float tintStart = 0.2f;
    [SerializeField] private Color warmColor = new Color(1f, 0.6f, 0.1f);
    [SerializeField] private Color hotColor = new Color(1f, 0.1f, 0.05f);
    [SerializeField] private Color emberColor = new Color(0.3f, 0.06f, 0.03f);
    [SerializeField] private float tintEmission = 3f;
    [Tooltip("Optionnel : fumée jouée pendant le refroidissement.")]
    [SerializeField] private ParticleSystem smoke;

    [Header("Lumière (optionnel)")]
    [SerializeField] private Light heatLight;
    [SerializeField] private float maxIntensity = 6f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private Crystal crystal;
    private float heat;
    private bool cooling;      // phase de refroidissement, gérée ici (ne dépend pas de Crystal)
    private bool stabilized;
    private bool warnedReset, warnedOutput;

    private Renderer[] tintRenderers;
    private Color[] baseColors;
    private MaterialPropertyBlock mpb;
    private bool tinted;
    private Quaternion visualRot;

    public float Heat01 => heatTime > 0f ? heat / heatTime : 0f;
    public bool IsCooling => cooling;

    private void Awake()
    {
        crystal = GetComponent<Crystal>();
        mpb = new MaterialPropertyBlock();

        if (visual == null)
            foreach (Transform t in GetComponentsInChildren<Transform>())
                if (t != transform && t.GetComponent<Renderer>() != null) { visual = t; break; }
        if (visual != null) visualRot = visual.localRotation;

        var list = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            if (!(r is ParticleSystemRenderer)) list.Add(r);
        tintRenderers = list.ToArray();
        baseColors = new Color[tintRenderers.Length];
        for (int i = 0; i < tintRenderers.Length; i++)
        {
            Material m = tintRenderers[i].sharedMaterial;
            baseColors[i] = m == null ? Color.white
                          : m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId)
                          : m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;
        }
    }

    private void Update()
    {
        if (stabilized) return;
        float dt = Time.deltaTime;   // s'arrête en pause (timeScale = 0)

        if (cooling)
        {
            if (!crystal.IsOverheated)
            {
                if (!warnedReset)
                {
                    warnedReset = true;
                    Debug.LogWarning($"{name} : la surchauffe a été effacée par Crystal pendant le refroidissement. " +
                                     "La ligne « overheated = false; » ne doit être QUE dans ResetState().", this);
                }
                crystal.SetOverheated(true);
            }

            heat -= coolRate * dt;
            if (heat <= 0f)
            {
                heat = 0f;
                cooling = false;
                crystal.SetOverheated(false);
            }
        }
        else if (crystal.OutputColor != Colors.None)
        {
            heat += dt;
            if (heat >= heatTime)
            {
                heat = heatTime;
                cooling = true;
                crystal.SetOverheated(true);
                if (crystal.OutputColor != Colors.None && !warnedOutput)
                {
                    warnedOutput = true;
                    Debug.LogWarning($"{name} : surchauffé mais OutputColor n'est pas None. " +
                                     "Ajoute « || overheated » dans la condition du getter OutputColor de Crystal.", this);
                }
            }
        }
        else heat = Mathf.Max(0f, heat - coolRate * dt);
    }

    // Visuels après Crystal.UpdateVisual, pour garder le dessus.
    private void LateUpdate()
    {
        UpdateTint();
        UpdateShiver();
        UpdateLight();
        if (smoke != null)
        {
            if (cooling && !smoke.isPlaying) smoke.Play();
            else if (!cooling && smoke.isPlaying) smoke.Stop();
        }
    }

    // Appelé par Charge quand le récepteur est plein : ne surchauffe plus.
    public void Stabilize()
    {
        stabilized = true;
        cooling = false;
        heat = 0f;
        crystal.SetOverheated(false);
    }

    // Pour un futur Restart
    public void ResetHeat()
    {
        stabilized = false;
        cooling = false;
        heat = 0f;
        crystal.SetOverheated(false);
    }

    private void UpdateTint()
    {
        float h = Mathf.InverseLerp(tintStart, 1f, Heat01);
        if (!cooling && h <= 0f)
        {
            if (tinted)
            {
                foreach (Renderer r in tintRenderers) r.SetPropertyBlock(null);
                tinted = false;
                crystal.UpdateVisual();   // rend la main au visuel normal du cristal
            }
            return;
        }

        tinted = true;
        for (int i = 0; i < tintRenderers.Length; i++)
        {
            Color col, em;
            if (cooling)
            {
                float b = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
                col = emberColor;
                em = hotColor * (0.3f + 0.7f * b) * tintEmission * Mathf.Max(0.2f, Heat01);
            }
            else
            {
                Color heatCol = Color.Lerp(warmColor, hotColor, h);
                col = Color.Lerp(baseColors[i], heatCol, h);
                em = heatCol * tintEmission * h;
            }

            tintRenderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, col);
            mpb.SetColor(ColorId, col);
            mpb.SetColor(EmissionId, em);
            tintRenderers[i].SetPropertyBlock(mpb);
        }
    }

    private void UpdateShiver()
    {
        if (visual == null) return;
        float start = heatTime - shiverTime;
        if (!cooling && !stabilized && heat > start && crystal.OutputColor != Colors.None)
        {
            float a = shiverAngle * Mathf.InverseLerp(start, heatTime, heat);
            visual.localRotation = visualRot * Quaternion.Euler(Random.Range(-a, a), Random.Range(-a, a) * 0.5f, Random.Range(-a, a));
        }
        else visual.localRotation = visualRot;
    }

    private void UpdateLight()
    {
        if (heatLight == null) return;
        heatLight.color = Color.Lerp(warmColor, hotColor, Heat01);
        float blink = cooling ? 0.5f + 0.5f * Mathf.Sin(Time.time * 14f) : 1f;
        heatLight.intensity = maxIntensity * Mathf.Max(Heat01, cooling ? 0.4f : 0f) * blink;
    }
}