using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Affiche les contrôles au lancement du jeu, puis les fait disparaître en fondu.
/// Crée lui-même son Canvas : il suffit de l'ajouter sur un GameObject vide.
/// </summary>
public class ControlsHint : MonoBehaviour
{
    [Header("Timing (secondes)")]
    [SerializeField] private float startDelay = 1f;
    [SerializeField] private float displayDuration = 6f;
    [SerializeField] private float fadeInDuration = 0.6f;
    [SerializeField] private float fadeOutDuration = 1.2f;

    [Header("Taille")]
    [Tooltip("1 = taille d'origine. Baisse la valeur pour un panneau plus petit.")]
    [SerializeField, Range(0.3f, 1.5f)] private float scale = 0.7f;

    [Header("Style (optionnel)")]
    [Tooltip("Laisser vide pour la police par défaut. Glisse ici la police du menu Pause si besoin.")]
    [SerializeField] private Font font;
    [SerializeField] private Color panelColor = new Color32(43, 33, 80, 225);
    [SerializeField] private Color borderColor = new Color32(111, 95, 208, 255);
    [SerializeField] private Color keyColor = new Color32(58, 46, 110, 255);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color subTextColor = new Color32(200, 192, 240, 255);

    private CanvasGroup group;

    private void Start()
    {
        BuildUI();
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        group.alpha = 0f;

        // unscaledTime : fonctionne même si le jeu est en pause (timeScale = 0)
        yield return WaitUnscaled(startDelay);
        yield return Fade(0f, 1f, fadeInDuration);
        yield return WaitUnscaled(displayDuration);
        yield return Fade(1f, 0f, fadeOutDuration);

        Destroy(group.transform.root.gameObject);
        Destroy(this);
    }

    private IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        group.alpha = to;
    }

    // ---------------------------------------------------------------- UI

    private void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("ControlsHintCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGO.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false; // ne bloque aucun clic

        // Panneau (en bas au centre)
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGO.transform, false);

        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 50f);
        panelRect.localScale = Vector3.one * scale; // réduit tout le panneau d'un coup

        var panelImg = panel.GetComponent<Image>();
        panelImg.color = panelColor;
        var border = panel.AddComponent<Outline>();
        border.effectColor = borderColor;
        border.effectDistance = new Vector2(2f, -2f);

        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(36, 36, 24, 28);
        vlg.spacing = 14f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Titre
        var title = CreateText(panel.transform, "Title", "Controls", 34, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
        SetHeight(title.gameObject, 50f);

        // Lignes de contrôles
        CreateRow(panel.transform, "Z  Q  S  D", "Move");
        CreateRow(panel.transform, "Right Click", "Move the camera");
        CreateRow(panel.transform, "Esc", "Pause menu");
    }

    private void CreateRow(Transform parent, string key, string description)
    {
        var row = new GameObject("Row_" + key, typeof(RectTransform));
        row.transform.SetParent(parent, false);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // "Touche" : petit bloc violet avec bordure
        var keyGO = new GameObject("Key", typeof(RectTransform), typeof(Image));
        keyGO.transform.SetParent(row.transform, false);
        keyGO.GetComponent<Image>().color = keyColor;
        var keyBorder = keyGO.AddComponent<Outline>();
        keyBorder.effectColor = borderColor;
        keyBorder.effectDistance = new Vector2(1.5f, -1.5f);

        var keyLE = keyGO.AddComponent<LayoutElement>();
        keyLE.minWidth = keyLE.preferredWidth = 190f;
        keyLE.minHeight = keyLE.preferredHeight = 44f;

        var keyText = CreateText(keyGO.transform, "Label", key, 26, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
        var kr = keyText.rectTransform;
        kr.anchorMin = Vector2.zero;
        kr.anchorMax = Vector2.one;
        kr.offsetMin = kr.offsetMax = Vector2.zero;

        // Description
        var desc = CreateText(row.transform, "Description", description, 26, FontStyle.Normal, subTextColor, TextAnchor.MiddleLeft);
        var descLE = desc.gameObject.AddComponent<LayoutElement>();
        descLE.minWidth = descLE.preferredWidth = 290f;
        descLE.minHeight = descLE.preferredHeight = 44f;
    }

    private Text CreateText(Transform parent, string name, string content, int size, FontStyle style, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var t = go.GetComponent<Text>();
        t.font = GetFont();
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private void SetHeight(GameObject go, float height)
    {
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = height;
    }

    private Font GetFont()
    {
        if (font != null) return font;
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }
}
