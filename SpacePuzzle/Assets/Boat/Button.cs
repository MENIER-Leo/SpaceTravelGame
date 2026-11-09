using UnityEngine;

public class Button : MonoBehaviour
{

    [Header("Cinématique")]
    public Transform camPoint;
    public CinematicManager cinematic;

    [Header("Bouton")]
    public Door door;
    private float offset = 0.13f;
    private bool isPressed = false;


    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isPressed)
        {
            isPressed = true;
            transform.position -= new Vector3(0, offset, 0);
            if (cinematic != null) cinematic.Play(door, camPoint);
            else door.Open();
        }
    }
}
