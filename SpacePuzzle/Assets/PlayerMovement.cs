using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float gravity = -15f;
    public float jumpForce = 8f;
    public float sphereRadius = 0.5f;
    public LayerMask groundLayer;

    private Vector3 velocity;
    private bool isGrounded;

    void Update()
    {
        RaycastHit hit;
        float rayLength = sphereRadius + 0.1f;

        if (Physics.Raycast(transform.position, Vector3.down, out hit, rayLength, groundLayer))
        {
            isGrounded = true;

            if (velocity.y < 0)
            {
                velocity.y = 0f;
                transform.position = new Vector3(transform.position.x, hit.point.y + sphereRadius, transform.position.z);
            }
        }
        else
        {
            isGrounded = false;
        }

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            velocity.y = jumpForce;
        }

        velocity.y += gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
    }
}