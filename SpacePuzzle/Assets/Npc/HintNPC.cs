using TMPro;
using UnityEngine;

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
        if (playerInside && !askedMore && Input.GetKeyDown(moreKey))
        {
            askedMore = true;
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
        Refresh();
        if (bubble != null) bubble.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerInside = false;
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

        if (solved && !string.IsNullOrEmpty(solvedLine))
        {
            bodyText.text = solvedLine;
        }
        else if (askedMore && !string.IsNullOrEmpty(secondLine))
        {
            bodyText.text = secondLine;
        }
        else
        {
            string prompt = string.IsNullOrEmpty(secondLine) || solved
                ? ""
                : $"\n<size=75%><color=#BBBBBB>[{moreKey}] Plus d'aide</color></size>";
            bodyText.text = firstLine + prompt;
        }
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
