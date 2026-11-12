using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Effets d'apparition des émetteurs du Phare.
/// À placer sur Region_Phare. Détecte tout seul quand un émetteur (nom "Emetteur...") est réactivé.
public class EmetteursVFX : MonoBehaviour
{
    [SerializeField, Tooltip("M_Particule_Lumiere (Particles/Unlit, Additive)")]
    private Material materiauLumiere;

    [Header("Couleurs")]
    [SerializeField] private Color bleu = new Color(0.3f, 0.6f, 1f);
    [SerializeField] private Color vert = new Color(0.3f, 1f, 0.45f);
    [SerializeField] private Color rouge = new Color(1f, 0.3f, 0.25f);

    [Header("Réglages")]
    [SerializeField, Tooltip("Durée de la traînée pendant la montée.")]
    private float dureeTrainee = 1.5f;
    [SerializeField, Tooltip("Temps max d'attente avant l'anneau final.")]
    private float attenteMax = 4f;

    private readonly Dictionary<Transform, bool> etats = new Dictionary<Transform, bool>();

    private void Start()
    {
        if (materiauLumiere == null)
            Debug.LogWarning("[EmetteursVFX] Aucun matériau : glisse M_Particule_Lumiere dans le champ.");

        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Emetteur"))
                etats[t] = t.gameObject.activeInHierarchy;
    }

    private void LateUpdate()
    {
        if (materiauLumiere == null) return;

        var cles = new List<Transform>(etats.Keys);
        foreach (Transform t in cles)
        {
            if (t == null) { etats.Remove(t); continue; }
            bool actif = t.gameObject.activeInHierarchy;
            if (actif && !etats[t]) StartCoroutine(Apparition(t));
            etats[t] = actif;
        }
    }

    private IEnumerator Apparition(Transform t)
    {
        Color c = CouleurPour(t.name);

        // 1. Surgissement
        VfxUtil.Glow(t.position, c, materiauLumiere, 2f, 0.35f);
        VfxUtil.Trail(t, c, materiauLumiere, dureeTrainee);

        // 2. On attend que l'émetteur ait fini de monter
        float attente = 0f, immobile = 0f;
        Vector3 derniere = t.position;
        while (attente < attenteMax && immobile < 0.15f)
        {
            yield return null;
            if (t == null) yield break;
            attente += Time.deltaTime;
            if ((t.position - derniere).sqrMagnitude < 0.000004f) immobile += Time.deltaTime;
            else immobile = 0f;
            derniere = t.position;
        }

        // 3. Arrivée
        VfxUtil.Ring(t.position, t.up, c, materiauLumiere);
        VfxUtil.Sparkles(t.position, t.up, c, materiauLumiere);
        VfxUtil.Glow(t.position, c, materiauLumiere, 3.5f, 0.5f);
    }

    private Color CouleurPour(string nom)
    {
        if (nom.Contains("Bleu")) return bleu;
        if (nom.Contains("Vert")) return vert;
        if (nom.Contains("Rouge")) return rouge;
        return Color.white;
    }
}
