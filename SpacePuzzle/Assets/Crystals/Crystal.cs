using System;
using System.Collections;
using UnityEngine;

public enum CrystalType { Emitter, Relay, Receiver, Mixer, Prism };

[Flags]
public enum Colors
{
    None = 0,
    Red = 1,
    Green = 2,
    Blue = 4,
    Yellow = Red | Green,
    Purple = Red | Blue,
    Cyan = Green | Blue,
    White = Red | Green | Blue
}

public class Crystal : MonoBehaviour
{
    // Événement partagé par tous les cristaux (static)
    public static event Action OnAnyCrystalChanged;


    private const int MixerRequiredInputs = 2;

    public CrystalType crystalType;

    public bool rotatable = true;

    public Transform beamOrigin;

    public Vector3 Heading => transform.forward;
    public Vector3 Up => transform.up;

    // Directions de sortie : 1 pour les cristaux classiques, 2 pour le prisme
    public Vector3[] GetHeadings()
    {
        if (crystalType != CrystalType.Prism) return new[] { transform.forward };

        return new[]
        {
        Quaternion.AngleAxis(-splitAngle, transform.up) * transform.forward,   // gauche
        Quaternion.AngleAxis( splitAngle, transform.up) * transform.forward    // droite
    };
    }

    [SerializeField] 
    private int inputCount;        // nombre de faisceaux reçus

    // Couleur émise par le cristal (pour les émetteurs et relais)
    [Header("Couleurs")]
    public Colors emittedColor;
    public Colors targetColor; // couleur cible pour les récepteurs

    [SerializeField] private Colors receivedColor;   // visible dans l'Inspector, remplie par le calcul
    public Colors ReceivedColor => receivedColor;

    public Colors OutputColor
    {
        get
        {
            if (isRotating || isAbsorbed) return Colors.None;
            switch (crystalType)
            {
                case CrystalType.Emitter:
                    return emittedColor;

                case CrystalType.Relay:
                case CrystalType.Prism:
                    return inputCount == 1 ? receivedColor : Colors.None;

                case CrystalType.Mixer:
                    return inputCount == MixerRequiredInputs ? receivedColor : Colors.None;

                default:                 // Receiver
                    return Colors.None;
            }
        }
    }

    public bool IsSatisfied =>
        crystalType == CrystalType.Receiver &&
        targetColor != Colors.None &&
        receivedColor == targetColor;

    public bool IsLit => receivedColor != Colors.None;

    [Header("Placement")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private Crystal aimTarget;

    [Header("Visuel")]
    [SerializeField] private Light glow;
    [SerializeField] private Renderer meshRenderer;
    [SerializeField] private float lightIntensity = 2f;
    [SerializeField] private float emissionIntensity = 3f;
    [SerializeField, Range(0f, 1f)] private float weakFactor = 0.25f;

    [Header("Rotation")]
    [SerializeField] private float stepAngle = 7.5f;
    [SerializeField] private float rotationSpeed = 15f;   // degrés par seconde
    [SerializeField] private float relightDelay = 0.15f;
    private bool initialRotatable;

    [Header("Prisme")]
    [SerializeField] private float splitAngle = 45f;   // demi-angle entre les deux sorties
    [SerializeField] private Crystal firstTarget;    // cible de la sortie gauche (outil éditeur)
    [SerializeField] private Crystal secondTarget;   // cible de la sortie droite (outil éditeur)


    [SerializeField] private GameObject highlight;
    private bool isAbsorbed;
    public bool IsAbsorbed => isAbsorbed;

    private Quaternion baseRotation;
    private int stepIndex;
    private bool isRotating;
    private bool turnHeld;
    private bool stepRequested;
    private int turnDirection = 1;
    private Coroutine turnRoutine;

    public bool IsRotating => isRotating;
    private int StepsPerTurn => Mathf.Max(1, Mathf.RoundToInt(360f / stepAngle));

    private MaterialPropertyBlock propertyBlock;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");


    public void RotateStep(float angle) 
    { 
        if (!rotatable) return; 
        transform.Rotate(0f, angle, 0f, Space.Self); 
        OnAnyCrystalChanged?.Invoke(); 
    }


    public Vector3 BeamStart()
    {         if (beamOrigin != null)
        {
            return beamOrigin.position;
        }
        else
        {
            return transform.position;
        }
    }

    private void Awake()
    {
        baseRotation = transform.rotation;
        stepIndex = 0;
        initialRotatable = rotatable;
    }

    private void OnDisable()
    {
        if (!isRotating) return;
        StopAllCoroutines();
        SetYaw(stepIndex * stepAngle);
        isRotating = false;
        turnHeld = false;
        stepRequested = false;
        turnRoutine = null;
    }

    public void SetHighlighted(bool on)
    {
        if (highlight != null) highlight.SetActive(on);
    }

    public void StartTurning(int direction)
    {
        if (!rotatable || isAbsorbed || !isActiveAndEnabled) return;
        turnDirection = direction >= 0 ? 1 : -1;
        turnHeld = true;
        stepRequested = true;
        if (turnRoutine == null) turnRoutine = StartCoroutine(TurnRoutine());
    }

    public void StopTurning()
    {
        turnHeld = false;
    }

    private IEnumerator TurnRoutine()
    {
        isRotating = true;
        OnAnyCrystalChanged?.Invoke();   // extinction

        while (true)
        {
            while (turnHeld || stepRequested)
            {
                stepRequested = false;
                yield return StartCoroutine(RotateOneStep(turnDirection));
            }

            float waited = 0f;
            while (waited < relightDelay && !stepRequested)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            if (!stepRequested) break;
        }

        isRotating = false;
        turnRoutine = null;
        OnAnyCrystalChanged?.Invoke();   // rallumage
    }

    private IEnumerator RotateOneStep(int direction)
    {
        float from = stepIndex * stepAngle;
        float to = from + direction * stepAngle;
        float duration = stepAngle / Mathf.Max(rotationSpeed, 0.01f);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetYaw(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }

        stepIndex = (stepIndex + direction + StepsPerTurn) % StepsPerTurn;
        SetYaw(stepIndex * stepAngle);   // rotation exacte, pas d'arrondi cumulé
    }

    private void SetYaw(float yaw)
    {
        transform.rotation = baseRotation * Quaternion.Euler(0f, yaw, 0f);
    }


    [ContextMenu("Rotate")]
    private void ApplyRotation()
    {
        if (Application.isPlaying) { StartTurning(1); StopTurning(); return; }
#if UNITY_EDITOR
    UnityEditor.Undo.RecordObject(transform, "Rotate crystal");
#endif
        transform.Rotate(0f, stepAngle, 0f, Space.Self);
    }


    public void ResetInput()
    {
        receivedColor = Colors.None;
        inputCount = 0;
    }

    // Remet le cristal dans son état de départ (orientation + verrou), sans recalcul
    public void ResetState()
    {
        StopAllCoroutines();
        turnRoutine = null;
        isRotating = false;
        turnHeld = false;
        stepRequested = false;
        stepIndex = 0;
        SetYaw(0f);
        rotatable = initialRotatable;
    }

    // Déclenche un recalcul unique des faisceaux depuis l'extérieur
    public static void NotifyChanged()
    {
        OnAnyCrystalChanged?.Invoke();
    }

    public void SetAbsorbed(bool value)
    {
        isAbsorbed = value;
    }


    public void AddInput(Colors color)
    {
        receivedColor |= color;
        inputCount++;
    }


    private void OnValidate()
    {
        emittedColor &= Colors.White;
        targetColor &= Colors.White;

        stepAngle = Mathf.Clamp(stepAngle, 1f, 90f);
        if (Mathf.Abs(360f / stepAngle - Mathf.Round(360f / stepAngle)) > 0.001f)
            Debug.LogWarning($"{name} : stepAngle ({stepAngle}) ne divise pas 360.");
        splitAngle = Mathf.Clamp(splitAngle, 5f, 90f);
    }


    [ContextMenu("Align to planet")]
    private void AlignToPlanetMenu() => AlignToPlanet(true);

    public bool AlignToPlanet(bool notify)
    {
        BeamManager manager = GetComponentInParent<BeamManager>();
        if (manager == null || manager.PlanetTransform == null)
        {
            Debug.LogWarning($"{name} : aucun BeamManager avec Planet dans les parents.");
            return false;
        }

#if UNITY_EDITOR
    if (!Application.isPlaying) UnityEditor.Undo.RecordObject(transform, "Align to planet");
#endif

        int mask = groundMask.value != 0 ? groundMask.value : LayerMask.GetMask("Planete");
        if (!TerrainSnap.Snap(transform, manager.PlanetTransform, mask))
        {
            Debug.LogWarning($"{name} : sol introuvable (collider ou layer de la planète ?).");
            return false;
        }

        baseRotation = transform.rotation;
        stepIndex = 0;

        if (notify && Application.isPlaying) OnAnyCrystalChanged?.Invoke();
        return true;
    }


    [ContextMenu("Aim at target")]
    private void AimAtTarget()
    {
        if (aimTarget == null)
        {
            Debug.LogWarning($"{name} : aucun Aim Target défini.");
            return;
        }

        Vector3 up = transform.up;
        Vector3 direction = Vector3.ProjectOnPlane(aimTarget.transform.position - transform.position, up);
        if (direction.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning($"{name} : cible exactement à l'opposé, direction indéfinie.");
            return;
        }


#if UNITY_EDITOR
    UnityEditor.Undo.RecordObject(transform, "Aim at target");
#endif
        transform.rotation = Quaternion.LookRotation(direction.normalized, up);

        if (crystalType == CrystalType.Prism)
            transform.rotation = Quaternion.AngleAxis(splitAngle, up) * transform.rotation;   // sortie gauche vers la cible

        baseRotation = transform.rotation;
        stepIndex = 0;

        if (Application.isPlaying) OnAnyCrystalChanged?.Invoke();
    }


    [ContextMenu("Setup prism targets (gauche + droite)")]
    private void SetupPrismTargets()
    {
        if (crystalType != CrystalType.Prism) { Debug.LogWarning($"{name} : n'est pas un prisme."); return; }
        if (firstTarget == null) { Debug.LogWarning($"{name} : First Target non assigné."); return; }

        BeamManager manager = GetComponentInParent<BeamManager>();
        if (manager == null) { Debug.LogWarning($"{name} : aucun BeamManager trouvé dans les parents."); return; }

        // 1. Recaler d'abord le prisme et la première cible (plus rien ne bougera au Play)
        AlignToPlanet(false);
        firstTarget.AlignToPlanet(false);

        // 2. Orienter la sortie gauche avec le même repère que TraceBeam
        Vector3 center = manager.PlanetTransform.position;
        Vector3 dir = (BeamStart() - center).normalized;
        Vector3 toFirst = Vector3.ProjectOnPlane(firstTarget.transform.position - center, dir);
        if (toFirst.sqrMagnitude < 1e-6f) { Debug.LogWarning($"{name} : First Target est à la verticale du prisme."); return; }

#if UNITY_EDITOR
    if (!Application.isPlaying) UnityEditor.Undo.RecordObject(transform, "Setup prism targets");
#endif
        transform.rotation = Quaternion.AngleAxis(splitAngle, dir) * Quaternion.LookRotation(toFirst, dir);
        baseRotation = transform.rotation;
        stepIndex = 0;

        // 3. Placer la deuxième cible sur la sortie droite
        if (secondTarget != null) PlaceSecondTarget();

        // Contrôle : doit afficher ~0°
        Vector3 left = Vector3.ProjectOnPlane(GetHeadings()[0], dir);
        Debug.Log($"{name} : écart sortie gauche / First Target = {Vector3.Angle(left, toFirst):F2}°");
    }



    [ContextMenu("Place second target (sortie droite)")]
    private void PlaceSecondTarget()
    {
        if (crystalType != CrystalType.Prism) { Debug.LogWarning($"{name} : n'est pas un prisme."); return; }
        if (secondTarget == null) { Debug.LogWarning($"{name} : Second Target non assigné."); return; }

        BeamManager manager = GetComponentInParent<BeamManager>();
        if (manager == null) { Debug.LogWarning($"{name} : aucun BeamManager trouvé dans les parents."); return; }

        Vector3 center = manager.PlanetTransform.position;
        Vector3 dir = (BeamStart() - center).normalized;
        Vector3 heading = Vector3.ProjectOnPlane(GetHeadings()[1], dir).normalized;

        // Garde la distance (angle sur la sphère) actuelle entre le prisme et la cible
        Vector3 targetOffset = secondTarget.transform.position - center;
        float angle = Vector3.Angle(dir, targetOffset);
        if (angle < 1f) { Debug.LogWarning($"{name} : la cible est trop proche du prisme."); return; }

        // Même grand cercle que TraceBeam
        Vector3 newDir = Quaternion.AngleAxis(angle, Vector3.Cross(dir, heading)) * dir;

#if UNITY_EDITOR
    if (!Application.isPlaying) UnityEditor.Undo.RecordObject(secondTarget.transform, "Place second target");
#endif
        secondTarget.transform.position = center + newDir * targetOffset.magnitude;

        if (!secondTarget.AlignToPlanet(true))
            Debug.LogWarning($"{secondTarget.name} : recalage sur le terrain impossible.");
    }



    // Appelée par BeamManager après chaque recalcul
    public void UpdateVisual()
    {

        if (isRotating || isAbsorbed) { ApplyGlow(Colors.None, 0f); return; }

        Colors color = Colors.None;
        float factor = 0f;

        switch (crystalType)
        {
            case CrystalType.Emitter:
                color = emittedColor;
                factor = 1f;
                break;

            case CrystalType.Relay:
            case CrystalType.Mixer:
            case CrystalType.Prism:
                if (OutputColor != Colors.None) { color = OutputColor; factor = 1f; }
                else if (IsLit) { color = receivedColor; factor = weakFactor; }
                break;

            case CrystalType.Receiver:
                if (IsSatisfied) { color = targetColor; factor = 1f; }
                else if (IsLit) { color = receivedColor; factor = weakFactor; }
                break;
        }

        ApplyGlow(color, factor);
    }



    private void ApplyGlow(Colors color, float factor)
    {
        bool on = color != Colors.None && factor > 0f;
        Color c = ColorsUtil.ToColor(color);

        if (glow != null)
        {
            glow.enabled = on;
            glow.color = c;
            glow.intensity = lightIntensity * factor;
        }

        if (meshRenderer != null)
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, on ? c * emissionIntensity * factor : Color.black);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Vector3 start = BeamStart();
        foreach (Vector3 h in GetHeadings()) Gizmos.DrawRay(start, h * 2f);
        if (crystalType == CrystalType.Prism && secondTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, secondTarget.transform.position);
        }
    }

}
