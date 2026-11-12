using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fondu plein écran qui survit au changement de scène.
// FonduScene.Create(couleur) → FadeTo(1, durée) → LoadAndReveal(scène, durée) : l'image réapparaît dans la nouvelle scène.
public class FonduScene : MonoBehaviour
{
    private CanvasGroup group;

    public float Alpha => group != null ? group.alpha : 0f;

    public static FonduScene Create(Color color)
    {
        GameObject go = new GameObject("Fondu_Scene", typeof(RectTransform));
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        GameObject im = new GameObject("Couleur", typeof(RectTransform));
        im.transform.SetParent(go.transform, false);
        RectTransform rt = (RectTransform)im.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = im.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        FonduScene f = go.AddComponent<FonduScene>();
        f.group = go.AddComponent<CanvasGroup>();
        f.group.alpha = 0f;
        f.group.blocksRaycasts = false;
        f.group.interactable = false;
        DontDestroyOnLoad(go);
        return f;
    }

    public IEnumerator FadeTo(float target, float time)
    {
        float start = group.alpha;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / time));
            yield return null;
        }
        group.alpha = target;
    }

    // Charge la scène sous le fondu, puis la révèle.
    public void LoadAndReveal(string scene, float revealTime) => StartCoroutine(LoadRoutine(scene, revealTime));

    // Révèle sans changer de scène.
    public void Reveal(float revealTime) => StartCoroutine(RevealRoutine(revealTime));

    private IEnumerator LoadRoutine(string scene, float revealTime)
    {
        group.alpha = 1f;
        AsyncOperation op = SceneManager.LoadSceneAsync(scene);
        while (!op.isDone) yield return null;
        yield return null;   // laisse les Start de la nouvelle scène s'exécuter
        yield return RevealRoutine(revealTime);
    }

    private IEnumerator RevealRoutine(float revealTime)
    {
        yield return FadeTo(0f, revealTime);
        Destroy(gameObject);
    }
}
