using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Fin du jeu : la caméra vole jusqu'à la lentille du Phare, qui se teinte de blanc et s'illumine
// pendant que la caméra s'en approche. La scène est éblouie, puis les crédits apparaissent
// UN BLOC À LA FOIS, puis retour au menu (si une scène de menu est renseignée).
// Déclenché par On Charged du foyer.
public class FinDuJeu : MonoBehaviour
{
    [Header("Phare")]
    [SerializeField] private Light lampLight;
    [SerializeField] private float lampIntensity = 60f;
    [Tooltip("La lentille qui se teinte de blanc.")]
    [SerializeField] private Renderer lampRenderer;
    [SerializeField] private float lampEmission = 8f;
    [SerializeField] private Color lampColor = new Color(1f, 0.97f, 0.88f);

    [Header("Cinématique")]
    [Tooltip("Point de vue sur la lentille (vide = la caméra reste où elle est).")]
    [SerializeField] private Transform viewPoint;
    [Tooltip("Si cette cinématique est en cours, la fin attend qu'elle se termine.")]
    [SerializeField] private CinematiquePhare cinematique;
    [SerializeField] private float flyTime = 2f;
    [SerializeField] private float flyLift = 6f;
    [Tooltip("Pause sur la lentille éteinte avant qu'elle s'allume.")]
    [SerializeField] private float beforeLight = 0.5f;
    [Tooltip("Mètres parcourus vers la lentille pendant l'éblouissement.")]
    [SerializeField] private float pushIn = 4f;

    [Header("Éblouissement")]
    [Tooltip("Volume global (Bloom + Post Exposure), poids animé de 0 à 1.")]
    [SerializeField] private Volume glareVolume;
    [Tooltip("Image blanche plein écran.")]
    [SerializeField] private CanvasGroup white;
    [SerializeField] private float lightUpTime = 2.5f;
    [SerializeField] private float whiteTime = 1.5f;

    [Header("Crédits (un bloc à la fois)")]
    [SerializeField] private CanvasGroup[] creditBlocks;
    [Tooltip("Blanc seul avant le premier bloc.")]
    [SerializeField] private float silenceTime = 1.5f;
    [SerializeField] private float blockFadeIn = 1.5f;
    [SerializeField] private float blockHoldTime = 2.5f;
    [SerializeField] private float blockFadeOut = 1f;
    [Tooltip("Pause entre deux blocs.")]
    [SerializeField] private float blockGap = 0.5f;
    [Tooltip("Le dernier bloc reste affiché.")]
    [SerializeField] private bool keepLast = true;
    [Tooltip("Temps avant le retour au menu, après le dernier bloc.")]
    [SerializeField] private float holdTime = 4f;
    [Tooltip("Vide = on reste sur les crédits.")]
    [SerializeField] private string menuScene = "";

    [Header("Scripts coupés pendant la fin")]
    [SerializeField] private MonoBehaviour[] disableDuring;

    private bool started;
    private BeamManager manager;
    private Material lampMat;
    private Color baseStart, emisStart;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        manager = FindFirstObjectByType<BeamManager>();
        if (white != null) { white.alpha = 0f; white.blocksRaycasts = false; }
        if (creditBlocks != null)
            foreach (CanvasGroup g in creditBlocks) if (g != null) g.alpha = 0f;
        if (glareVolume != null) glareVolume.weight = 0f;
        if (lampLight != null) lampLight.intensity = 0f;

        if (lampRenderer != null)
        {
            lampMat = lampRenderer.material;   // copie propre à la lentille
            baseStart = lampMat.HasProperty(BaseColorId) ? lampMat.GetColor(BaseColorId) : Color.gray;
            emisStart = lampMat.HasProperty(EmissionId) ? lampMat.GetColor(EmissionId) : Color.black;
        }
    }

    private void OnDestroy()
    {
        if (lampMat != null) Destroy(lampMat);
    }

    public void Play()
    {
        if (started) return;
        started = true;
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        while (cinematique != null && cinematique.IsPlaying) yield return null;

        if (disableDuring != null)
            foreach (MonoBehaviour mb in disableDuring) if (mb != null) mb.enabled = false;

        // 1. Vol jusqu'à la lentille
        Camera cam = Camera.main;
        Transform ct = cam != null ? cam.transform : null;
        if (ct != null && viewPoint != null) yield return Fly(ct, viewPoint.position, viewPoint.rotation);
        yield return new WaitForSeconds(beforeLight);

        Vector3 camStart = ct != null ? ct.position : Vector3.zero;
        Vector3 push = ct != null ? ct.forward * pushIn : Vector3.zero;
        float total = lightUpTime + whiteTime;

        // 2. La lentille blanchit et s'illumine, la caméra avance vers elle
        for (float t = 0f; t < lightUpTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / lightUpTime);
            if (lampLight != null) lampLight.intensity = lampIntensity * k;
            SetLamp(k);
            if (glareVolume != null) glareVolume.weight = 0.6f * k;
            Push(ct, camStart, push, t / total);
            yield return null;
        }
        SetLamp(1f);

        // 3. Éblouissement : le blanc prend le relais
        for (float t = 0f; t < whiteTime; t += Time.deltaTime)
        {
            float k = t / whiteTime;
            if (glareVolume != null) glareVolume.weight = Mathf.Lerp(0.6f, 1f, k);
            if (white != null) white.alpha = k * k;
            Push(ct, camStart, push, (lightUpTime + t) / total);
            yield return null;
        }
        if (white != null) white.alpha = 1f;

        // 4. Silence, puis les crédits un par un
        yield return new WaitForSeconds(silenceTime);
        if (creditBlocks != null)
            for (int i = 0; i < creditBlocks.Length; i++)
            {
                CanvasGroup g = creditBlocks[i];
                if (g == null) continue;
                yield return Fade(g, 0f, 1f, blockFadeIn);
                if (keepLast && i == creditBlocks.Length - 1) break;
                yield return new WaitForSeconds(blockHoldTime);
                yield return Fade(g, 1f, 0f, blockFadeOut);
                yield return new WaitForSeconds(blockGap);
            }

        yield return new WaitForSeconds(holdTime);

        // 5. Retour au menu
        if (string.IsNullOrEmpty(menuScene)) yield break;
        if (Application.CanStreamedLevelBeLoaded(menuScene)) SceneManager.LoadScene(menuScene);
        else Debug.LogWarning($"{name} : la scène « {menuScene} » n'est pas dans la liste des scènes du build.");
    }

    private static IEnumerator Fade(CanvasGroup g, float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            g.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time));
            yield return null;
        }
        g.alpha = to;
    }

    private static void Push(Transform ct, Vector3 start, Vector3 push, float f)
    {
        if (ct == null) return;
        f = Mathf.Clamp01(f);
        ct.position = start + push * (f * f);   // accélère vers la lumière
    }

    private void SetLamp(float k)
    {
        if (lampMat == null) return;
        if (k > 0f) lampMat.EnableKeyword("_EMISSION");
        if (lampMat.HasProperty(BaseColorId)) lampMat.SetColor(BaseColorId, Color.Lerp(baseStart, Color.white, k));
        if (lampMat.HasProperty(EmissionId)) lampMat.SetColor(EmissionId, Color.Lerp(emisStart, lampColor * lampEmission, k));
    }

    // Vol en arc autour de la planète (ne traverse pas le sol).
    private IEnumerator Fly(Transform t, Vector3 pos, Quaternion rot)
    {
        Vector3 p = t.position;
        Quaternion r = t.rotation;
        bool arc = manager != null && manager.PlanetTransform != null;
        Vector3 c = arc ? manager.PlanetTransform.position : Vector3.zero;

        for (float x = 0f; x < flyTime; x += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, x / flyTime);
            Vector3 at;
            if (arc)
            {
                Vector3 a = p - c, b = pos - c;
                float radius = Mathf.Lerp(a.magnitude, b.magnitude, k) + Mathf.Sin(k * Mathf.PI) * flyLift;
                at = c + Vector3.Slerp(a.normalized, b.normalized, k) * radius;
            }
            else at = Vector3.Lerp(p, pos, k);

            t.SetPositionAndRotation(at, Quaternion.Slerp(r, rot, k));
            yield return null;
        }
        t.SetPositionAndRotation(pos, rot);
    }
}