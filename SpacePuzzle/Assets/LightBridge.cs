using UnityEngine;

public class LightBridge : MonoBehaviour
{
    public bool isActivated = false; // Passe à true quand le puzzle est résolu
    public float deploySpeed = 5.0f;
    private Vector3 targetScale;

    void Start()
    {
        targetScale = transform.localScale;

        if (!isActivated)
        {
            transform.localScale = new Vector3(targetScale.x, targetScale.y, 0f);
        }
    }

    void Update()
    {
        if (isActivated && transform.localScale.z < targetScale.z)
        {
            float newZ = Mathf.MoveTowards(transform.localScale.z, targetScale.z, deploySpeed * Time.deltaTime);
            transform.localScale = new Vector3(targetScale.x, targetScale.y, newZ);
        }
    }

    public void ActivateBridge()
    {
        isActivated = true;
    }
}