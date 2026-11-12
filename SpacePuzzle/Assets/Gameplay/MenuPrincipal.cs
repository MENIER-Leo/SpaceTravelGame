using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
#endif

/// Menu principal : planète en fond, boutons Play / Quit.
/// Clic droit sur le composant > "Construire le menu" pour générer l'interface.
public class MenuPrincipal : MonoBehaviour
{
    [Header("Jeu")]
    [SerializeField] private string gameScene = "Game_Scene";

    [Header("Fond")]
    [SerializeField, Tooltip("Centre de la planète. Vide = caméra fixe.")]
    private Transform orbitCenter;
    [SerializeField, Tooltip("Degrés par seconde autour de la planète (0 = fixe).")]
    private float orbitSpeed = 2f;

    [Header("Transitions")]
    [SerializeField] private float uiFadeInTime = 1.5f;
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private float fadeOutTime = 1f;
    [SerializeField] private float revealTime = 1.5f;

    [Header("Construction")]
    [SerializeField] private string title = "SpacePuzzle";

    [Header("Références (remplies par le builder)")]
    [SerializeField] private Camera menuCamera;
    [SerializeField] private CanvasGroup uiGroup;

    private bool starting;

    private void Start()
    {
        // Sécurité si on arrive depuis une pause ou la fin du jeu
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (menuCamera == null) menuCamera = Camera.main;
        if (uiGroup != null) StartCoroutine(FadeUI(0f, 1f, uiFadeInTime));
    }

    private void Update()
    {
        if (orbitCenter != null && menuCamera != null && orbitSpeed != 0f)
            menuCamera.transform.RotateAround(orbitCenter.position, Vector3.up, orbitSpeed * Time.unscaledDeltaTime);
    }

    public void Play()
    {
        if (starting) return;
        if (!Application.CanStreamedLevelBeLoaded(gameScene))
        {
            Debug.LogError($"[MenuPrincipal] La scène \"{gameScene}\" n'est pas dans la Scene List (File > Build Profiles).");
            return;
        }
        starting = true;
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        if (uiGroup != null)
        {
            uiGroup.interactable = false;
            StartCoroutine(FadeUI(uiGroup.alpha, 0f, fadeOutTime));
        }

        FonduScene fondu = FonduScene.Create(fadeColor);
        yield return fondu.FadeTo(1f, fadeOutTime);
        fondu.LoadAndReveal(gameScene, revealTime);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator FadeUI(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            uiGroup.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        uiGroup.alpha = to;
    }

#if UNITY_EDITOR
    [ContextMenu("Construire le menu")]
    private void Build()
    {
        Undo.RecordObject(this, "Construire le menu");
        if (menuCamera == null) menuCamera = Camera.main;

        Transform old = transform.Find("Canvas_Menu");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // --- Canvas ---
        var canvasGO = new GameObject("Canvas_Menu", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Construire le menu");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        uiGroup = canvasGO.GetComponent<CanvasGroup>();

        // --- Titre ---
        var titleGO = new GameObject("Titre", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(canvasGO.transform, false);
        var trt = (RectTransform)titleGO.transform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -140f);
        trt.sizeDelta = new Vector2(1600f, 220f);
        var ttxt = titleGO.GetComponent<TextMeshProUGUI>();
        ttxt.text = title;
        ttxt.fontSize = 140;
        ttxt.fontStyle = FontStyles.Bold;
        ttxt.characterSpacing = 8f;
        ttxt.alignment = TextAlignmentOptions.Center;
        ttxt.color = Color.white;
        ttxt.raycastTarget = false;

        // --- Boutons ---
        UIButton play = CreateButton(canvasGO.transform, "Bouton_Play", "Play", new Vector2(0f, -120f));
        UIButton quit = CreateButton(canvasGO.transform, "Bouton_Quit", "Quit", new Vector2(0f, -260f));
        UnityEventTools.AddPersistentListener(play.onClick, Play);
        UnityEventTools.AddPersistentListener(quit.onClick, Quit);

        // --- EventSystem ---
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Construire le menu");
        }

        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("[MenuPrincipal] Menu construit.");
    }

    private static UIButton CreateButton(Transform parent, string name, string label, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UIButton));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(420f, 100f);
        rt.anchoredPosition = pos;

        go.GetComponent<Image>().color = Color.white; // teinté par le ColorBlock

        var btn = go.GetComponent<UIButton>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0f, 0f, 0f, 0.45f);
        cb.highlightedColor = new Color(0.25f, 0.55f, 1f, 0.7f);
        cb.pressedColor = new Color(0.15f, 0.35f, 0.8f, 0.9f);
        cb.selectedColor = cb.normalColor;
        cb.fadeDuration = 0.12f;
        btn.colors = cb;

        var txtGO = new GameObject("Texte", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(go.transform, false);
        var trt = (RectTransform)txtGO.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var txt = txtGO.GetComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 54;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.raycastTarget = false;

        return btn;
    }
#endif
}
