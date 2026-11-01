using UnityEngine;

// Cristal noir : maintenir E pour réinitialiser le puzzle de la planète
public class ResetCrystal : MonoBehaviour
{
    [SerializeField] private PuzzleController puzzle;     // rempli automatiquement si vide (parent)
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField] private float decaySpeed = 3f;       // vitesse de vidage de la jauge au relâchement

    [Header("Visuel")]
    [SerializeField] private GameObject highlight;
    [SerializeField] private Light glow;
    [SerializeField] private Renderer meshRenderer;       // enfant Mesh
    [SerializeField] private Transform meshTransform;     // enfant Mesh (tremblement)
    [SerializeField] private Color glowColor = new Color(0.47f, 0.16f, 1f);

    [Header("Charge")]
    [SerializeField] private float chargeLightIntensity = 4f;
    [SerializeField] private float chargeEmission = 2f;
    [SerializeField] private float chargeShake = 0.04f;

    [Header("Explosion (reset)")]
    [SerializeField] private float burstLightIntensity = 40f;
    [SerializeField] private float burstRange = 18f;
    [SerializeField] private float burstEmission = 12f;
    [SerializeField] private float burstShake = 0.08f;
    [SerializeField] private float burstDuration = 0.9f;
    [SerializeField]
    private AnimationCurve burstCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.06f, 1f), new Keyframe(0.5f, 0.6f), new Keyframe(1f, 0f));

    private float charge;          // 0 → 1
    private bool holding;
    private float burstTimer = -1f;
    private float baseRange;
    private Vector3 meshBasePosition;
    private MaterialPropertyBlock block;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public bool CanInteract => puzzle != null && puzzle.CanReset;
    public float Charge => charge;

    private void Awake()
    {
        if (puzzle == null) puzzle = GetComponentInParent<PuzzleController>();
        if (puzzle == null) Debug.LogWarning($"{name} : aucun PuzzleController trouvé dans les parents.");

        block = new MaterialPropertyBlock();
        if (glow != null) baseRange = glow.range;
        if (meshTransform != null) meshBasePosition = meshTransform.localPosition;

        UpdateVisual();
    }

    private void Update()
    {
        if (holding && CanInteract)
        {
            charge += Time.deltaTime / Mathf.Max(holdDuration, 0.01f);

            if (charge >= 1f)
            {
                charge = 0f;
                holding = false;     // il faudra relâcher et réappuyer pour recommencer
                burstTimer = 0f;     // déclenche l'explosion de lumière
                puzzle.ResetPuzzle();
            }
        }
        else
        {
            charge = Mathf.Max(0f, charge - decaySpeed * Time.deltaTime);
        }

        if (burstTimer >= 0f)
        {
            burstTimer += Time.deltaTime;
            if (burstTimer >= burstDuration) burstTimer = -1f;
        }

        UpdateVisual();
    }

    public void BeginHold()
    {
        if (CanInteract) holding = true;
    }

    public void EndHold()
    {
        holding = false;
    }

    public void SetHighlighted(bool on)
    {
        if (highlight != null) highlight.SetActive(on);
    }

    private void UpdateVisual()
    {
        float c = charge * charge;   // montée qui s'accélère vers la fin
        float burst = burstTimer >= 0f ? burstCurve.Evaluate(burstTimer / burstDuration) : 0f;

        // Lumière
        if (glow != null)
        {
            float intensity = Mathf.Max(chargeLightIntensity * c, burstLightIntensity * burst);
            glow.enabled = intensity > 0.001f;
            glow.color = glowColor;
            glow.intensity = intensity;
            glow.range = Mathf.Lerp(baseRange, burstRange, burst);
        }

        // Émission du mesh
        if (meshRenderer != null)
        {
            float emission = Mathf.Max(chargeEmission * c, burstEmission * burst);
            meshRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, glowColor * emission);
            meshRenderer.SetPropertyBlock(block);
        }

        // Tremblement
        if (meshTransform != null)
        {
            float shake = Mathf.Max(chargeShake * c, burstShake * burst);
            meshTransform.localPosition = meshBasePosition + Random.insideUnitSphere * shake;
        }
    }
}