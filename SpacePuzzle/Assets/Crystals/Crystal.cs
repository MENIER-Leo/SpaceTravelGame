using System;
using System.Collections;
using UnityEngine;

public enum CrystalType { Emitter, Relay, Receiver };

public class Crystal : MonoBehaviour
{

    public CrystalType crystalType;


    public Vector3 Heading => transform.forward;
    public Vector3 Up => transform.up;

    public void RotateStep(float angle)
    {
        if (crystalType != CrystalType.Relay) return;
        transform.Rotate(0f, angle, 0f, Space.Self);
    }
}
