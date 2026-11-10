using TMPro;
using UnityEngine;
using System.Collections;

// PNJ habitant : affiche une bulle quand le joueur approche.
// Réplique 1 à l'arrivée, réplique 2 sur demande (touche F),
// réplique "après résolution" une fois le puzzle terminé.
public class HintNPC : MonoBehaviour
{
    [Header("Personnage")]
    public string npcName = "Odile";
    [TextArea(2, 5)] public string firstLine;
    [TextArea(2, 5)] public string secondLine;
    [TextArea(2, 5)] public string solvedLine;

    [Header("Interaction")]
    public KeyCode moreKey = KeyCode.F;
    public string playerTag = "Player";

    [Header("Bulle")]
    public GameObject bubble;
    public TMP_Text nameText;
    public TMP_Text bodyText;

    [Header("Planète")]
    public Transform planetCenter;

    [Header("Texte défilant")]
    public float charsPerSecond = 40f;
    public float sentencePause = 0.25f;
    public float commaPause = 0.1f;

    private Coroutine typing;
    private bool isTyping;

    private bool playerInside;
    private bool askedMore;
    private bool solved;
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
        if (bubble != null) bubble.SetActive(false);
    }

    void Update()
    {
        if (!playerInside || !Input.GetKeyDown(moreKey)) return;

        if (isTyping)
        {
            SkipTyping(); // F pendant l'écriture : tout afficher
        }
        else if (!askedMore && !solved && !string.IsNullOrEmpty(secondLine))
        {
            askedMore = true; // F après l'écriture : réplique suivante
            Refresh();
        }
    }

    void LateUpdate()
    {
        if (bubble != null && bubble.activeSelf && cam != null)
            bubble.transform.rotation = cam.transform.rotation;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerInside = true;
        if (bubble != null) bubble.SetActive(true);
        Refresh();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerInside = false;
        StopTyping();
        if (bubble != null) bubble.SetActive(false);
    }

    public void OnPuzzleSolved()
    {
        solved = true;
        if (playerInside) Refresh();
    }

    bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag)) return true;
        Rigidbody rb = other.attachedRigidbody;
        return rb != null && rb.CompareTag(playerTag);
    }

    void Refresh()
    {
        if (nameText != null) nameText.text = npcName;
        if (bodyText == null) return;

        string line;
        if (solved && !string.IsNullOrEmpty(solvedLine))
        {
            line = solvedLine;
        }
        else if (askedMore && !string.IsNullOrEmpty(secondLine))
        {
            line = secondLine;
        }
        else
        {
            string prompt = string.IsNullOrEmpty(secondLine) || solved
                ? ""
                : $"\n<size=75%><color=#BBBBBB>[{moreKey}] Continuer</color></size>";
            line = firstLine + prompt;
        }

        StopTyping();
        typing = StartCoroutine(TypeText(line));
    }

    IEnumerator TypeText(string line)
    {
        isTyping = true;
        bodyText.text = line;
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate(); // calcule le nombre de lettres visibles (hors balises)

        int total = bodyText.textInfo.characterCount;
        float delay = 1f / Mathf.Max(1f, charsPerSecond);

        for (int i = 1; i <= total; i++)
        {
            bodyText.maxVisibleCharacters = i;

            char c = bodyText.textInfo.characterInfo[i - 1].character;
            float wait = delay;
            if (c == '.' || c == '!' || c == '?') wait += sentencePause;
            else if (c == ',' || c == ';' || c == ':') wait += commaPause;

            yield return new WaitForSeconds(wait);
        }

        bodyText.maxVisibleCharacters = 99999;
        isTyping = false;
        typing = null;
    }

    void SkipTyping()
    {
        StopTyping();
        if (bodyText != null) bodyText.maxVisibleCharacters = 99999;
    }

    void StopTyping()
    {
        if (typing != null) StopCoroutine(typing);
        typing = null;
        isTyping = false;
    }

    [ContextMenu("Aligner sur la planète")]
    void AlignToPlanet()
    {
        if (planetCenter == null)
        {
            Debug.LogWarning($"{name} : assigne Planet Center avant d'aligner.");
            return;
        }
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(transform, "Aligner PNJ sur la planète");
#endif
        Vector3 up = (transform.position - planetCenter.position).normalized;
        transform.rotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
    }
}
