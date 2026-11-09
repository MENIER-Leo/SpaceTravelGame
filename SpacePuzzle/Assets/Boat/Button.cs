using UnityEngine;

public class Button : MonoBehaviour
{


    public Door door;
    private float offset = 0.13f;
    private bool isPressed = false;


    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isPressed)
        {
            isPressed = true;
            transform.position -= new Vector3(0, offset, 0);
            door.Open();
        }
    }
}
