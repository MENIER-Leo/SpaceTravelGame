using UnityEngine;
using UnityEngine.Events;


public class RegionReset : MonoBehaviour
{
    [SerializeField] private string regionProperty;
    [SerializeField] private RegionLight regionLight;
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
