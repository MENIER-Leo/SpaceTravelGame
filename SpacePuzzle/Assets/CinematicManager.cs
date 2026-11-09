using System.Collections;
using UnityEngine;

public class CinematicManager : MonoBehaviour
{
    [Header("Références")]
    public Camera cam;
    public PlayerMovement playerMovement;
    public CameraFollow cameraFollow;

    [Header("Durées (secondes)")]
    public float goDuration = 0.6f;        // trajet caméra -> point de vue
    public float pauseBeforeOpen = 0.2f;   // petite attente avant que la porte bouge
    public float doorOpenDuration = 1f;    // temps que met la porte à s'ouvrir
    public float holdAfterOpen = 0.3f;     // on regarde la porte ouverte un instant
    public float returnDuration = 0.5f;    // trajet point de vue -> joueur

    public bool IsPlaying { get; private set; }

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    public void Play(Door door, Transform viewPoint)
    {
        if (door == null) return;

        if (IsPlaying || viewPoint == null || cam == null)
        {
            door.Open();
            return;
        }

        StartCoroutine(PlayRoutine(door, viewPoint));
    }

    IEnumerator PlayRoutine(Door door, Transform viewPoint)
    {
        IsPlaying = true;

        if (playerMovement != null) playerMovement.enabled = false;
        if (cameraFollow != null) cameraFollow.enabled = false;


        Transform camT = cam.transform;
        Vector3 startPos = camT.position;
        Quaternion startRot = camT.rotation;

        yield return StartCoroutine(MoveCamera(startPos, startRot,
                                               viewPoint.position, viewPoint.rotation,
                                               goDuration));

        yield return new WaitForSeconds(pauseBeforeOpen);
        door.Open();

        yield return new WaitForSeconds(doorOpenDuration + holdAfterOpen);

        yield return StartCoroutine(MoveCamera(viewPoint.position, viewPoint.rotation,
                                               startPos, startRot,
                                               returnDuration));

        if (cameraFollow != null) cameraFollow.enabled = true;
        if (playerMovement != null) playerMovement.enabled = true;

        IsPlaying = false;
    }

    IEnumerator MoveCamera(Vector3 fromPos, Quaternion fromRot,
                           Vector3 toPos, Quaternion toRot, float duration)
    {
        Transform camT = cam.transform;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            camT.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, t),
                                        Quaternion.Slerp(fromRot, toRot, t));

            yield return null;
        }

        // Position finale exacte (évite un léger décalage dû aux arrondis).
        camT.SetPositionAndRotation(toPos, toRot);
    }
}