using System.Collections;
using UnityEngine;

// Fait apparaître et pousser l'objet (pont-liane...).
public class Croissance : MonoBehaviour
{
    [SerializeField] private float duration = 2.5f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private bool grown;
    private bool hasFull;
    private Vector3 fullScale;

    public void Grow()
    {
        if (grown) return;
        grown = true;
        if (!hasFull) { fullScale = transform.localScale; hasFull = true; }
        transform.localScale = fullScale * 0.01f;
        gameObject.SetActive(true);
        StartCoroutine(GrowRoutine());
    }

    private IEnumerator GrowRoutine()
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            transform.localScale = fullScale * Mathf.Max(0.01f, curve.Evaluate(t / duration));
            yield return null;
        }
        transform.localScale = fullScale;
        Physics.SyncTransforms();
        Crystal.NotifyChanged();   // le pont compte maintenant comme du sol pour les rayons
    }

    // Pour un futur Restart : cache à nouveau l'objet
    public void ResetGrowth()
    {
        StopAllCoroutines();
        if (hasFull) transform.localScale = fullScale;
        grown = false;
        gameObject.SetActive(false);
    }
}
