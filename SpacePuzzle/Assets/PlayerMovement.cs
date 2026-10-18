using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float gravity = 5f;
    public float jumpForce = 100f;
    public float sphereRadius = 0.5f;
    public LayerMask groundLayer;

    public CameraFollow cameraFollow;  

    private Vector3 velocity;
    private const float SPEED = 5f;
    private bool isGrounded;
    private Vector3 horizontalMove = Vector3.zero;
    private float verticalSpeed = 0f;
    private Vector3 up;


    private Transform pTransform;


    void Start()
    {

        pTransform = GameObject.FindWithTag("Boule").transform;
    }


    void handleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            verticalSpeed = jumpForce;
            isGrounded = false;
        }
        if (Input.GetKey(KeyCode.W))
            horizontalMove += transform.forward;
        if (Input.GetKey(KeyCode.S))
            horizontalMove += -transform.forward;
        if(Input.GetKey(KeyCode.A))
            horizontalMove += -transform.right;
        if(Input.GetKey(KeyCode.D))
            horizontalMove += transform.right;
        horizontalMove = Vector3.ClampMagnitude(horizontalMove, 1f) * SPEED;
    }

    void applyGravity()
    {

        up = GetUp();

        if (!isGrounded)
            verticalSpeed = Mathf.Min(verticalSpeed + gravity * Time.deltaTime, 100f);

        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 100f * Time.deltaTime);

    }

    Vector3 GetUp()
    {
        return (transform.position - pTransform.position).normalized;
    }

    void Update()
    {
        RaycastHit hit;

        handleInput();
        applyGravity();   
        
        Vector3 tangentMove = Vector3.ProjectOnPlane(horizontalMove, transform.up);
        velocity = (verticalSpeed * transform.up + tangentMove) * Time.deltaTime;
        horizontalMove = Vector3.zero;
        transform.position += velocity;

        up = GetUp();

        float rayLength = sphereRadius + 0.3f;
        bool isHit = Physics.Raycast(transform.position, -up, out hit, rayLength, groundLayer);

        if (verticalSpeed <= 0f && isHit)
        {
            isGrounded = true;
            verticalSpeed = 0;
            transform.position = hit.point + hit.normal * sphereRadius;
        }
        else
        {
            isGrounded = false;
        }
    }
}