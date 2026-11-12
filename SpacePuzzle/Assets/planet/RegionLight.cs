using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Rallume progressivement une région de la planète (propriété Lit du Shader Graph)
// et fait monter le Soleil d'un palier par région allumée, quel que soit l'ordre.
public class RegionLight : MonoBehaviour
{
    [Header("Planète")]
    [Tooltip("Vide = tous les Renderers enfants de cet objet.")]
    [SerializeField] private Renderer[] renderers;
    [Tooltip("Noms de référence (Reference) des propriétés Lit dans le Shader Graph.")]
    [SerializeField] private string[] regionProperties = { "_Lit_Rouge", "_Lit_Vert", "_Lit_Bleu" };
    [SerializeField] private float duration = 2f;

    [Header("Soleil")]
    [SerializeField] private Light sun;
    [Tooltip("Intensité selon le nombre de régions allumées : 0, 1, 2, 3.")]
    [SerializeField] private float[] sunSteps = { 0.6f, 0.8f, 1f, 1.2f };

    [Header("Événements")]
    public UnityEvent onAllRegionsLit;
    public UnityEvent<string> onRegionLit;

    private readonly HashSet<string> lit = new HashSet<string>();
    private readonly Dictionary<string, float> values = new Dictionary<string, float>();
    private MaterialPropertyBlock block;
    private Coroutine sunRoutine;

    public int LitCount => lit.Count;

    public bool IsLit(string property) => lit.Contains(property);

    private void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();

        block = new MaterialPropertyBlock();

        // Vérifie que les noms correspondent bien au shader
        if (renderers.Length > 0 && renderers[0].sharedMaterial != null)
            foreach (string p in regionProperties)
                if (!renderers[0].sharedMaterial.HasProperty(p))
                    Debug.LogWarning($"{name} : le matériau n'a pas de propriété « {p} » (vérifie la Reference dans le Shader Graph).");

        ResetRegions();
    }

    // À appeler depuis un UnityEvent (On Lit), avec le nom de la propriété : _Lit_Bleu…
    public void LightRegion(string property)
    {
        if (!values.ContainsKey(property))
        {
            Debug.LogWarning($"{name} : propriété « {property} » absente de Region Properties.");
            return;
        }
        if (!lit.Add(property)) return;   // déjà allumée

        onRegionLit?.Invoke(property);

        StartCoroutine(AnimateRegion(property));

        if (sun != null && sunSteps.Length > 0)
        {
            float target = sunSteps[Mathf.Min(lit.Count, sunSteps.Length - 1)];
            if (sunRoutine != null) StopCoroutine(sunRoutine);
            sunRoutine = StartCoroutine(AnimateSun(target));
        }

        if (lit.Count == regionProperties.Length) onAllRegionsLit?.Invoke();
    }

    private IEnumerator AnimateRegion(string property)
    {
        float from = values[property];
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            values[property] = Mathf.Lerp(from, 1f, Mathf.SmoothStep(0f, 1f, t / duration));
            Apply();
            yield return null;
        }
        values[property] = 1f;
        Apply();
    }

    private IEnumerator AnimateSun(float target)
    {
        float from = sun.intensity;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            sun.intensity = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        sun.intensity = target;
        sunRoutine = null;
    }

    // Écrit les valeurs courantes sur chaque morceau de la planète (sans toucher au matériau)
    private void Apply()
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            foreach (KeyValuePair<string, float> kv in values) block.SetFloat(kv.Key, kv.Value);
            r.SetPropertyBlock(block);
        }
    }

    // Tout éteint, Soleil au palier 0 (départ ou Restart sans rechargement)
    [ContextMenu("Reset regions")]
    public void ResetRegions()
    {
        StopAllCoroutines();
        sunRoutine = null;
        lit.Clear();
        values.Clear();
        foreach (string p in regionProperties) values[p] = 0f;
        Apply();
        if (sun != null && sunSteps.Length > 0) sun.intensity = sunSteps[0];
    }

    [ContextMenu("Test : tout allumer")]
    private void TestLightAll()
    {
        if (!Application.isPlaying) return;
        foreach (string p in regionProperties) LightRegion(p);
    }
}
