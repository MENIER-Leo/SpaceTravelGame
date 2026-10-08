using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float gravity = -15f;
    public float jumpForce = 8f;
    public float sphereRadius = 0.5f;
    public LayerMask groundLayer;

    private Vector3 velocity;
    private const float SPEED = 5f;
    private bool isGrounded;

    private Transform pTransform;


    void Start()
    {

        pTransform = GameObject.FindWithTag("Boule").transform;
    }


    void handleInput()
    {
        if(Input.GetKeyDown(KeyCode.Space) && isGrounded)
            velocity.y = jumpForce;
        if (Input.GetKey(KeyCode.W))
            velocity += Vector3.forward;
        if (Input.GetKey(KeyCode.S))
            velocity += Vector3.back;
        if(Input.GetKey(KeyCode.A))
            velocity += Vector3.left;
        if(Input.GetKey(KeyCode.D))
            velocity += Vector3.right;
    }

    void normalizeHorizontalVelocity()
    {
        float norm = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
        if (norm != 0)
            velocity = new Vector3(velocity.x / norm * SPEED, velocity.y, velocity.z / norm * SPEED);
    }

    void applyGravity()
    {
        Vector3 up = transform.position - pTransform.position;

        velocity += up.normalized * gravity;

        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 100f * Time.deltaTime);
    }

    void Update()
    {
        RaycastHit hit;
        float rayLength = sphereRadius + 0.1f;

        handleInput();
        //normalizeHorizontalVelocity();
        applyGravity();

        Debug.Log(Physics.Raycast(transform.position, -transform.up, out hit, rayLength, groundLayer));

        if (Physics.Raycast(transform.position, -transform.up, out hit, rayLength, groundLayer))
        {
            isGrounded = true;

            transform.position = hit.point + Vector3.up * sphereRadius;

            /*if(velocity.y < 0)
            {
                transform.position = new Vector3(transform.position.x, hit.point.y + sphereRadius, transform.position.z);
            }*/
        }
        else
        {
            isGrounded = false;
        }


        transform.position += velocity * Time.deltaTime;
        velocity = Vector3.zero;
    }
}