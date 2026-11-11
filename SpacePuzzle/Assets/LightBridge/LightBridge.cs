using UnityEngine;

public class LightBridge : MonoBehaviour
{
    public bool isActivated = false;
    public float deploySpeed = 5.0f;

    private Vector3 targetScale;
    private MeshRenderer meshRenderer;
    private Collider bridgeCollider;

    void Start()
    {
        targetScale = transform.localScale;
        meshRenderer = GetComponent<MeshRenderer>();
        bridgeCollider = GetComponent<Collider>();

        if (!isActivated)
        {
            // Échelle minimale à 0.01f (JAMAIS 0f pour éviter le bug d'éclairage infini "Soleil")
            transform.localScale = new Vector3(targetScale.x, targetScale.y, 0.01f);
            if (meshRenderer != null) meshRenderer.enabled = false;
            if (bridgeCollider != null) bridgeCollider.enabled = false;
        }
    }

    void Update()
    {
        if (isActivated)
        {
            // Réactive l'affichage et la collision dès que le pont s'allume
            if (meshRenderer != null && !meshRenderer.enabled) meshRenderer.enabled = true;
            if (bridgeCollider != null && !bridgeCollider.enabled) bridgeCollider.enabled = true;

            if (transform.localScale.z < targetScale.z)
            {
                float newZ = Mathf.MoveTowards(transform.localScale.z, targetScale.z, deploySpeed * Time.deltaTime);
                transform.localScale = new Vector3(targetScale.x, targetScale.y, newZ);
            }
        }
    }

    public void ActivateBridge()
    {
        isActivated = true;
    }
}