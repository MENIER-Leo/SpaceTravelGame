using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    [Header("Gravité et Saut")]
    public float gravity = 20f;
    public float jumpForce = 8f;
    public float maxFallSpeed = 10f;


    [Header("Collisions")]
    public float sphereRadius = 0.5f;
    public const float raycastMargin = 0.1f;
    public const float maxSlopeAngle = 50f; // angle max de pente pour marcher dessus
    public float stepHeight = 0.5f;     // d'où on regarde vers le sol
    public float slideSpeed = 4f; //vitesse de glissement sur les pentes raides
    public float minDistanceFromCenter = 5f;
    public LayerMask groundLayer;


    //Mouvements
    private const float SPEED = 5f;
    private bool isGrounded = false;
    private Vector3 horizontalMove;
    private Vector3 lastSafePosition;
    private float verticalSpeed = 0f;
    private Vector3 up;
    


    private Transform planet;


    void Start()
    {

        planet = GameObject.FindWithTag("Boule").transform;
    }


    void handleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            verticalSpeed = jumpForce;
            isGrounded = false;
        }
        if (Input.GetKey(KeyCode.W)) horizontalMove += transform.forward;
        if (Input.GetKey(KeyCode.S)) horizontalMove -= transform.forward;
        if(Input.GetKey(KeyCode.A)) horizontalMove -= transform.right;
        if(Input.GetKey(KeyCode.D)) horizontalMove += transform.right;
        horizontalMove = Vector3.ClampMagnitude(horizontalMove, 1f) * SPEED;
    }

    //Retourne le haut par rapport à la planète (la normale de la planète)
    Vector3 GetUp()
    {
        return (transform.position - planet.position).normalized;
    }

    //Retourne vrai si la marche est possible (angle entre la normale et le haut inférieur à maxSlopeAngle)
    bool IsWalkable(Vector3 surfaceNormal, Vector3 up)
    {
        return Vector3.Angle(surfaceNormal, up) <= maxSlopeAngle;
    }

    //Aligne le joueur avec la planète (le haut du joueur est aligné avec le haut de la planète)
    void AlignToPlanet(Vector3 up, float dt)
    {
        Quaternion target = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 15f * dt);
    }

    //Bloque le joueur contre les murs (si le joueur se déplace vers un mur, il est bloqué), si pente raide il glisse
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

    void handleVerticalMovement(float step, Vector3 up, float dt)
    {

        //Si on monte : pas besoin de chercher le sol
        if (step > 0f)
        {
            transform.position += up * step;
            isGrounded = false;
            return;
        }

        //Si on descend ou qu'on se trouve au sol: on cherche le sol et on applique la gravité

        float fall = -step;
        Vector3 origin = transform.position + up * stepHeight;
        float range = stepHeight + sphereRadius + raycastMargin + fall;
        

        if (!Physics.Raycast(origin, -up, out RaycastHit hit, range, groundLayer))
        {
            transform.position += up * step;   // rien sous nous : on tombe
            isGrounded = false;
            return;
        }


        Vector3 restPosition = hit.point + up * sphereRadius;  // position "posé sur le sol"

        if (IsWalkable(hit.normal, up))
        {
            transform.position = restPosition;  // on se colle au sol (suit les bosses)
            verticalSpeed = 0f;
            isGrounded = true;
        }
        else
        {
            Vector3 next = transform.position + up * step;

            if (Vector3.Dot(restPosition - next, up) > 0f)
            {
                next = restPosition;
                verticalSpeed = 0f;
            }

            Vector3 downhill = Vector3.ProjectOnPlane(-up, hit.normal).normalized;
            transform.position = next + downhill * slideSpeed * dt;
            isGrounded = false;
        }

    }

    // Filet de sécurité : si on se retrouve dans la planète, retour au dernier sol.
    void RecoverIfInsidePlanet()
    {
        if (isGrounded)
        {
            lastSafePosition = transform.position;
        }
        else if ((transform.position - planet.position).magnitude < minDistanceFromCenter)
        {
            transform.position = lastSafePosition;
            verticalSpeed = 0f;
        }
    }

    void Update()
    {

        float dt = Mathf.Min(Time.deltaTime, 0.05f); // évite les pics de lag

        handleInput();
        Vector3 up = GetUp();
        AlignToPlanet(up, dt);

        // 1. Avancer
        Vector3 move = Vector3.ProjectOnPlane(horizontalMove, up) * dt;
        horizontalMove = Vector3.zero;
        transform.position += BlockWalls(move, up);

        

        // 2. Gravité (vitesse moyenne : le saut ne dépend plus du framerate)
        float previousSpeed = verticalSpeed;
        if (!isGrounded)
            verticalSpeed = Mathf.Max(verticalSpeed - gravity * dt, -maxFallSpeed);
        float verticalStep = (previousSpeed + verticalSpeed) * 0.5f * dt;

        // 3. Monter / descendre / se coller au sol
        up = GetUp();
        handleVerticalMovement(verticalStep, up, dt);

        RecoverIfInsidePlanet();
    }
}