using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float gravity = 5f;
    public float jumpForce = 100f;
    public float sphereRadius = 0.5f;
    public LayerMask groundLayer;

    private const float SPEED = 5f;

    private bool isGrounded;

    private Vector3 horizontalMove = Vector3.zero;
    private float verticalSpeed = 0f;

    private Vector3 up;
    private const float raycastMargin = 0.1f;
    private const float maxSlopeAngle = 45f; // angle max de pente pour marcher dessus
    public float stepHeight = 0.5f;     // d'où on regarde vers le sol


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
            verticalSpeed = Mathf.Max(verticalSpeed + gravity * Time.deltaTime, -30f);

        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 100f * Time.deltaTime);

    }

    Vector3 GetUp()
    {
        return (transform.position - pTransform.position).normalized;
    }

    bool IsWalkable(Vector3 surfaceNormal, Vector3 up)
    {
        return Vector3.Angle(surfaceNormal, up) <= maxSlopeAngle;
    }

    Vector3 BlockWalls(Vector3 move, Vector3 up)
    {
        if (move.sqrMagnitude < 1e-7f) return move;

        Vector3 origin = transform.position - up * (sphereRadius * 0.8f);
        float checkDistance = sphereRadius + move.magnitude;

        if (Physics.Raycast(origin, move.normalized,
                            out RaycastHit wall, checkDistance, groundLayer)
            && !IsWalkable(wall.normal, up))
        {
            move = Vector3.ProjectOnPlane(move, wall.normal);
            move = Vector3.ProjectOnPlane(move, up);
        }
        return move;
    }

    bool FindGround(Vector3 up, out RaycastHit hit)
    {
;
        Vector3 origin = transform.position + up * stepHeight;
        float distance = stepHeight + sphereRadius + raycastMargin;

        return Physics.Raycast(origin, -up, out hit, distance, groundLayer) && IsWalkable(hit.normal, up);
    }

    void Update()
    {

        handleInput();
        applyGravity();
        up = GetUp();

        Vector3 move = Vector3.ProjectOnPlane(horizontalMove, up) * Time.deltaTime;
        horizontalMove = Vector3.zero;

        move = BlockWalls(move, up);

        transform.position += move + up * verticalSpeed * Time.deltaTime;

        up = GetUp();

        if (verticalSpeed <= 0f && FindGround(up, out RaycastHit hit))
        {
            isGrounded = true;
            verticalSpeed = 0;
            transform.position = hit.point + up * sphereRadius;
        }
        else
        {
            isGrounded = false;
        }
    }
}