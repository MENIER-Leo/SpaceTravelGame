using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class VictorySequence : MonoBehaviour
{
    [Header("Contrôles coupés pendant la séquence")]
    [SerializeField] private MonoBehaviour[] disableDuring;

    [Header("Rythme")]
    [SerializeField] private float startDelay = 0.5f;

    [Header("Lumière")]
    [SerializeField] private Light sun;
    [SerializeField] private float sunTargetIntensity = 1.5f;
    [SerializeField] private float lightDuration = 2f;
    [SerializeField, Tooltip("Objets désactivés au départ, allumés un par un.")]
    private GameObject[] lamps;
    [SerializeField] private float lampInterval = 0.15f;

    [Header("Éclat des cristaux")]
    [SerializeField] private float flashPeak = 6f;
    [SerializeField] private float flashDuration = 1.2f;
    [SerializeField] private float flashWaveInterval = 0.06f;
    [SerializeField] private float flashLead = 0.8f;

    [Header("Cinématique")]
    [SerializeField] private Camera cam;
    [SerializeField] private Transform viewPoint;
    [SerializeField] private float goDuration = 1.2f;
    [SerializeField] private float holdDuration = 2f;
    [SerializeField] private float returnDuration = 1f;

    [Header("Révélation")]
    [SerializeField] private UnityEvent onReveal;

    [Header("Message")]
    [SerializeField] private CanvasGroup messageGroup;
    [SerializeField] private TMP_Text messageText;
    [SerializeField, TextArea] private string message = "Les cristaux brillent à nouveau";
    [SerializeField] private float messageFade = 0.8f;
    [SerializeField] private float messageHold = 2.5f;

    [Header("Son")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip chime;

    public bool IsPlaying { get; private set; }
    private bool played;

    private void Start()
    {
        if (messageGroup != null) messageGroup.alpha = 0f;
    }

    [ContextMenu("Tester la séquence")]
    public void Play()
    {
        if (!Application.isPlaying) return;
        if (played) return;
        played = true;
        StartCoroutine(Run());
    }

    private void FlashCrystals()
    {
        Crystal[] all = FindObjectsByType<Crystal>(FindObjectsSortMode.None);
        Vector3 origin = cam != null ? cam.transform.position : transform.position;

        // Du plus proche au plus lointain, pour créer une vague.
        System.Array.Sort(all, (a, b) =>
            (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude));

        for (int i = 0; i < all.Length; i++)
            all[i].Flash(flashPeak, flashDuration, i * flashWaveInterval);
    }

    private IEnumerator Run()
    {
        IsPlaying = true;

        // Couper les contrôles en mémorisant leur état.
        bool[] states = new bool[disableDuring.Length];
        for (int i = 0; i < disableDuring.Length; i++)
        {
            if (disableDuring[i] == null) continue;
            states[i] = disableDuring[i].enabled;
            disableDuring[i].enabled = false;
        }

        yield return new WaitForSeconds(startDelay);

        if (audioSource != null && chime != null) audioSource.PlayOneShot(chime);
        FlashCrystals();
        yield return new WaitForSeconds(flashLead);
        StartCoroutine(RaiseSun());
        StartCoroutine(LightLamps());

        // Aller vers le point de vue.
        bool useCam = cam != null && viewPoint != null;
        Vector3 startPos = Vector3.zero;
        Quaternion startRot = Quaternion.identity;
        if (useCam)
        {
            startPos = cam.transform.position;
            startRot = cam.transform.rotation;
            yield return MoveCamera(startPos, startRot, viewPoint.position, viewPoint.rotation, goDuration);
        }

        // Révélation : le pont apparaît sous les yeux du joueur.
        onReveal.Invoke();
        if (messageGroup != null) StartCoroutine(ShowMessage());

        yield return new WaitForSeconds(holdDuration);

        // Retour au joueur.
        if (useCam)
            yield return MoveCamera(cam.transform.position, cam.transform.rotation, startPos, startRot, returnDuration);

        for (int i = 0; i < disableDuring.Length; i++)
            if (disableDuring[i] != null) disableDuring[i].enabled = states[i];

        IsPlaying = false;
    }

    private IEnumerator MoveCamera(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            cam.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));
            yield return null;
        }
        cam.transform.SetPositionAndRotation(toPos, toRot);
    }

    private IEnumerator RaiseSun()
    {
        if (sun == null) yield break;
        float from = sun.intensity;
        for (float t = 0f; t < lightDuration; t += Time.deltaTime)
        {
            sun.intensity = Mathf.Lerp(from, sunTargetIntensity, Mathf.SmoothStep(0f, 1f, t / lightDuration));
            yield return null;
        }
        sun.intensity = sunTargetIntensity;
    }

    private IEnumerator LightLamps()
    {
        if (lamps == null) yield break;
        foreach (GameObject lamp in lamps)
        {
            if (lamp != null) lamp.SetActive(true);
            yield return new WaitForSeconds(lampInterval);
        }
    }

    private IEnumerator ShowMessage()
    {
        if (messageText != null) messageText.text = message;
        yield return Fade(0f, 1f);
        yield return new WaitForSeconds(messageHold);
        yield return Fade(1f, 0f);
    }

    private IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < messageFade; t += Time.deltaTime)
        {
            messageGroup.alpha = Mathf.Lerp(from, to, t / messageFade);
            yield return null;
        }
        messageGroup.alpha = to;
    }
}
