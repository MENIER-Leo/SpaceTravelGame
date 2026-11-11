using System.Collections;
using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    [Header("Planète")]
    [SerializeField] private Transform planetCenter;
    [SerializeField, Tooltip("Au-delà de cette distance du centre, le joueur est considéré comme tombé.")]
    private float maxDistance = 60f;
    [SerializeField, Tooltip("En dessous de cette distance, il est passé sous la surface.")]
    private float minDistance = 10f;
    [SerializeField, Tooltip("Lecture seule : distance actuelle au centre, pour régler les deux valeurs.")]
    private float currentDistance;

    [Header("Points sûrs")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheck = 1.5f;
    [SerializeField] private float saveInterval = 1f;
    [SerializeField] private float maxSafeSpeed = 8f;

    [Header("Fondu")]
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float blackHold = 0.3f;
    [SerializeField, Tooltip("Scripts coupés pendant la téléportation (déplacement, interactions...).")]
    private MonoBehaviour[] disableDuring;

    private Vector3 safePos;
    private Quaternion safeRot;
    private Vector3 lastPos;
    private float timer;
    private bool respawning;
    private Rigidbody rb;
    private CharacterController cc;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        cc = GetComponent<CharacterController>();

        Transform start = spawnPoint != null ? spawnPoint : transform;
        safePos = start.position;
        safeRot = start.rotation;
        lastPos = transform.position;

        if (fade != null) fade.alpha = 0f;
    }

    private void Update()
    {
        if (planetCenter == null || respawning) return;

        // Chute dans l'espace ou passage sous la surface.
        currentDistance = Vector3.Distance(transform.position, planetCenter.position);
        if (currentDistance > maxDistance || currentDistance < minDistance)
        {
            StartCoroutine(Teleport(safePos, safeRot));
            return;
        }

        // Mémoriser régulièrement un point sûr.
        float speed = (transform.position - lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPos = transform.position;

        timer += Time.deltaTime;
        if (timer >= saveInterval)
        {
            timer = 0f;
            if (speed < maxSafeSpeed && IsGrounded())
            {
                safePos = transform.position;
                safeRot = transform.rotation;
            }
        }
    }

    private bool IsGrounded()
    {
        Vector3 up = (transform.position - planetCenter.position).normalized;
        return Physics.Raycast(transform.position + up * 0.5f, -up, groundCheck + 0.5f,
                               groundMask, QueryTriggerInteraction.Ignore);
    }

    // Appelé par le bouton "Débloquer" du menu pause.
    public void Unstuck()
    {
        if (respawning) return;
        Transform target = spawnPoint != null ? spawnPoint : transform;
        StartCoroutine(Teleport(target.position, target.rotation));
    }

    private IEnumerator Teleport(Vector3 pos, Quaternion rot)
    {
        respawning = true;

        bool[] states = new bool[disableDuring.Length];
        for (int i = 0; i < disableDuring.Length; i++)
        {
            if (disableDuring[i] == null) continue;
            states[i] = disableDuring[i].enabled;
            disableDuring[i].enabled = false;
        }

        yield return Fade(0f, 1f);
        MoveTo(pos, rot);
        yield return new WaitForSecondsRealtime(blackHold);
        yield return Fade(1f, 0f);

        for (int i = 0; i < disableDuring.Length; i++)
            if (disableDuring[i] != null) disableDuring[i].enabled = states[i];

        respawning = false;
    }

    private void MoveTo(Vector3 pos, Quaternion rot)
    {
        if (cc != null) cc.enabled = false;

        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.position = pos;
            rb.rotation = rot;
        }
        transform.SetPositionAndRotation(pos, rot);

        if (cc != null) cc.enabled = true;
        lastPos = pos;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fade == null) yield break;
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            fade.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        fade.alpha = to;
    }
}
