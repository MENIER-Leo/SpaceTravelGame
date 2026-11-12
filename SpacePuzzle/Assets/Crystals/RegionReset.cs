using UnityEngine;
using UnityEngine.Events;

// À placer sur l'ancre d'une région (Region_Rouge…). Remet SA région dans son état de départ, sans toucher aux autres.
// Le ResetCrystal placé sous l'ancre l'utilise automatiquement. Inutilisable une fois la région allumée.
public class RegionReset : MonoBehaviour
{
    [Tooltip("Propriété de la région dans RegionLight : _Lit_Rouge, _Lit_Vert, _Lit_Bleu. Vide = jamais désactivé (Phare).")]
    [SerializeField] private string regionProperty;
    [Tooltip("Vide = cherché automatiquement dans la scène.")]
    [SerializeField] private RegionLight regionLight;
    [Tooltip("Actions supplémentaires au reset (si une mécanique n'est pas gérée ci-dessous).")]
    public UnityEvent onReset;

    public bool IsSolved => regionLight != null && !string.IsNullOrEmpty(regionProperty) && regionLight.IsLit(regionProperty);
    public bool CanReset => !IsSolved && !PauseMenu.IsPaused;

    private void Awake()
    {
        if (regionLight == null) regionLight = FindFirstObjectByType<RegionLight>();
        if (regionLight == null && !string.IsNullOrEmpty(regionProperty))
            Debug.LogWarning($"{name} : aucun RegionLight, le reset ne se désactivera pas une fois la région résolue.");
    }

    public void ResetRegion()
    {
        if (!CanReset) return;

        // Objets actifs uniquement : les émetteurs du Phare encore cachés ne sont pas touchés.
        foreach (Crystal c in GetComponentsInChildren<Crystal>()) c.ResetState();
        foreach (Croissance g in GetComponentsInChildren<Croissance>()) g.ResetGrowth();
        foreach (Surchauffe h in GetComponentsInChildren<Surchauffe>()) h.ResetHeat();
        foreach (Charge ch in GetComponentsInChildren<Charge>()) ch.ResetCharge();
        foreach (Phototropisme p in GetComponentsInChildren<Phototropisme>()) p.ResetTimer();

        onReset?.Invoke();
        Crystal.NotifyChanged();
    }
}
