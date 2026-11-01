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
                case CrystalType.Mixer:
                    return receivedColor;

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

}
