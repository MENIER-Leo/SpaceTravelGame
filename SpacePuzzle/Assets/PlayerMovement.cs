using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float gravity = 5f;
    public float jumpForce = 100f;
    public float sphereRadius = 0.5f;
    public LayerMask groundLayer;

    private Vector3 velocity;
    private const float SPEED = 5f;
    private bool isGrounded;
    private bool isJumping = false;
    private Vector3 horizontalMove = Vector3.zero;
    private float verticalSpeed = 0f;


    private Transform pTransform;


    void Start()
    {

        pTransform = GameObject.FindWithTag("Boule").transform;
    }


    void handleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            isJumping = true;
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
        Vector3 up = transform.position - pTransform.position;

        Debug.Log("Avant verticalSpeed: " + verticalSpeed);

        verticalSpeed = Mathf.Min(verticalSpeed + gravity * Time.deltaTime, 100f);

        Debug.Log("Apres verticalSpeed: " + verticalSpeed);

        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 100f * Time.deltaTime);

    }

    void Update()
    {
        RaycastHit hit;
        float rayLength = sphereRadius + 0.1f;

        handleInput();


        bool isHit = Physics.Raycast(transform.position, -transform.up, out hit, rayLength, groundLayer);

        if(!isHit)
        {
            isGrounded = false;
        }

        if (!isGrounded)
        {
            applyGravity();
        }


        if(isJumping && isGrounded)
        {
            verticalSpeed += jumpForce;
        }

        if (isJumping && verticalSpeed < 0) isJumping = false;

        if (!isJumping && isHit)
        {
            isGrounded = true;

            if(verticalSpeed < 0)
            {
                verticalSpeed = 0;
                transform.position = hit.point + transform.up * sphereRadius;
            }
        }

        velocity = (verticalSpeed * transform.up + horizontalMove) * Time.deltaTime;
        horizontalMove = Vector3.zero;
        transform.position += velocity;
    }
}