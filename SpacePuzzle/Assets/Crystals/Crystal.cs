using System;
using System.Collections;
using UnityEngine;

public enum CrystalType { Emitter, Relay, Receiver, Mixer };

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
            switch (crystalType)
            {
                case CrystalType.Emitter:
                    return emittedColor;

                case CrystalType.Relay:
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

    [Header("Placement (éditeur)")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private Crystal aimTarget;

    [Header("Visuel")]
    [SerializeField] private Light glow;
    [SerializeField] private Renderer meshRenderer;
    [SerializeField] private float lightIntensity = 2f;
    [SerializeField] private float emissionIntensity = 3f;
    [SerializeField, Range(0f, 1f)] private float weakFactor = 0.25f;

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

    [ContextMenu("Rotate")]
    private void ApplyRotation()
    {
        RotateStep(90f);
    }

    public void ResetInput()
    {
        receivedColor = Colors.None;
        inputCount = 0;
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
    }

    [ContextMenu("Align to planet")]
    private void AlignToPlanet()
    {
        BeamManager manager = GetComponentInParent<BeamManager>();
        if (manager == null || manager.PlanetTransform == null)
        {
            Debug.LogWarning($"{name} : aucun BeamManager avec Planet dans les parents.");
            return;
        }

        Vector3 center = manager.PlanetTransform.position;
        Vector3 normal = (transform.position - center).normalized;
        if (normal == Vector3.zero)
        {
            Debug.LogWarning($"{name} : le cristal est au centre de la planète.");
            return;
        }

        int mask = groundMask.value != 0 ? groundMask.value : LayerMask.GetMask("Planete");
        float probe = (transform.position - center).magnitude + 100f;
        Vector3 start = center + normal * probe;

        Physics.SyncTransforms();
        if (!Physics.Raycast(start, -normal, out RaycastHit hit, probe, mask, QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning($"{name} : sol introuvable (collider ou layer de la planète ?).");
            return;
        }

        float pivotHeight = 0f;
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null) pivotHeight = (box.size.y * 0.5f - box.center.y) * transform.lossyScale.y;

#if UNITY_EDITOR
    UnityEditor.Undo.RecordObject(transform, "Align to planet");
#endif
        transform.position = hit.point + normal * pivotHeight;
        transform.rotation = Quaternion.FromToRotation(transform.up, normal) * transform.rotation;
        if (Application.isPlaying) OnAnyCrystalChanged?.Invoke();
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
        if (Application.isPlaying) OnAnyCrystalChanged?.Invoke();
    }


    // Appelée par BeamManager après chaque recalcul
    public void UpdateVisual()
    {
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

}
