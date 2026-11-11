using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Surveille un récepteur et réagit quand il est satisfait (bonne couleur reçue).
// Usages : dégel, verrous, croissance, allumage d'une région.
public class ReceiverWatcher : MonoBehaviour
{
    [Tooltip("Récepteur surveillé. Vide = le Crystal de cet objet.")]
    [SerializeField] private Crystal receiver;

    [Tooltip("Coché : reste déclenché même si le rayon repart ailleurs.")]
    [SerializeField] private bool latch = true;

    [Header("Dégel / déverrouillage")]
    [Tooltip("Cristaux rendus rotatifs au déclenchement.")]
    [SerializeField] private Crystal[] crystalsToUnlock;
    [Tooltip("Objets qui fondent (rétrécissent) puis disparaissent : blocs de glace…")]
    [SerializeField] private GameObject[] objectsToHide;
    [SerializeField] private float meltDuration = 0.8f;

    [Header("Événements")]
    public UnityEvent onLit;
    [Tooltip("Appelé seulement si Latch est décoché, quand le récepteur s'éteint.")]
    public UnityEvent onUnlit;

    private BeamManager manager;
    private bool triggered;
    private Vector3[] initialScales;

    public bool Triggered => triggered;

    private void Reset()
    {
        receiver = GetComponent<Crystal>();
    }

    private void Awake()
    {
        if (receiver == null) receiver = GetComponent<Crystal>();
        if (receiver != null) manager = receiver.GetComponentInParent<BeamManager>();

        if (manager == null)
            Debug.LogWarning($"{name} : aucun BeamManager trouvé au-dessus du récepteur.");

        if (objectsToHide == null) objectsToHide = new GameObject[0];
        initialScales = new Vector3[objectsToHide.Length];
        for (int i = 0; i < objectsToHide.Length; i++)
            if (objectsToHide[i] != null) initialScales[i] = objectsToHide[i].transform.localScale;
    }

    private void OnEnable()
    {
        if (manager != null) manager.OnRecomputed += Check;
    }

    private void OnDisable()
    {
        if (manager != null) manager.OnRecomputed -= Check;
    }

    // Appelée après chaque recalcul des faisceaux
    private void Check()
    {
        if (receiver == null) return;
        bool satisfied = receiver.IsSatisfied;

        if (satisfied && !triggered)
        {
            triggered = true;
            Unlock();
            onLit?.Invoke();
        }
        else if (!satisfied && triggered && !latch)
        {
            triggered = false;
            onUnlit?.Invoke();
        }
    }

    private void Unlock()
    {
        if (crystalsToUnlock != null)
            foreach (Crystal c in crystalsToUnlock)
                if (c != null) c.rotatable = true;

        for (int i = 0; i < objectsToHide.Length; i++)
            if (objectsToHide[i] != null && objectsToHide[i].activeSelf)
                StartCoroutine(Melt(objectsToHide[i]));
    }

    private IEnumerator Melt(GameObject target)
    {
        Transform t = target.transform;
        Vector3 start = t.localScale;

        for (float time = 0f; time < meltDuration; time += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, time / meltDuration);
            t.localScale = Vector3.Lerp(start, Vector3.zero, k);
            yield return null;
        }

        target.SetActive(false);
    }

    // Remet tout comme au départ (pour un Restart sans rechargement de scène)
    [ContextMenu("Reset watcher")]
    public void ResetWatcher()
    {
        StopAllCoroutines();
        triggered = false;

        if (crystalsToUnlock != null)
            foreach (Crystal c in crystalsToUnlock)
                if (c != null) c.rotatable = false;

        for (int i = 0; i < objectsToHide.Length; i++)
        {
            if (objectsToHide[i] == null) continue;
            objectsToHide[i].transform.localScale = initialScales[i];
            objectsToHide[i].SetActive(true);
        }
    }

    // Pour tester sans résoudre le puzzle (en Play)
    [ContextMenu("Test : déclencher")]
    private void ForceTrigger()
    {
        if (!Application.isPlaying || triggered) return;
        triggered = true;
        Unlock();
        onLit?.Invoke();
    }
}
