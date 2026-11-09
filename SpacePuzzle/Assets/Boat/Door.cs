using UnityEngine;
using System.Collections;

public class Door : MonoBehaviour
{
    private Vector3 offset = new Vector3(0, -3.1f, 0);
    private float duration = 1.0f;
    private bool isOpen = false;

    [ContextMenu("Ouvrir")]
    public void Open()
    {
        if (isOpen) return;
        
        isOpen = true;
        StartCoroutine(MoveDoor(offset));
        this.GetComponent<UnityEngine.AI.NavMeshObstacle>().enabled = false;
    }

    private IEnumerator MoveDoor(Vector3 offset)
    {
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + offset;
        float elapsedTime = 0;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            transform.position = Vector3.Lerp(startPosition, targetPosition, Mathf.SmoothStep(0, 1, t));
            
            yield return null;
        }
        transform.position = targetPosition;
    }
}
