using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Gravité et Saut")]
    public float gravity = 20f;
    public float jumpForce = 8f;
    public float maxFallSpeed = 10f;

    [HideInInspector] public bool isOnBridge = false;
    [HideInInspector] public Vector3 bridgeUp;

    [Header("Collisions")]
    public float sphereRadius = 0.5f;
    public const float raycastMargin = 0.1f;
    public const float maxSlopeAngle = 50f; // angle max de pente pour marcher dessus
    public float stepHeight = 0.5f;         // d'où on regarde vers le sol
    public float slideSpeed = 4f;           // vitesse de glissement sur les pentes raides
    public float minDistanceFromCenter = 5f;
    public LayerMask groundLayer;

    // Mouvements
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

<<<<<<< Updated upstream

    void handleInput()
=======
    public void handleInput(Transform camera)
>>>>>>> Stashed changes
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            verticalSpeed = jumpForce;
            isGrounded = false;
        }
<<<<<<< Updated upstream
        if (Input.GetKey(KeyCode.W)) horizontalMove += transform.forward;
        if (Input.GetKey(KeyCode.S)) horizontalMove -= transform.forward;
        if(Input.GetKey(KeyCode.A)) horizontalMove -= transform.right;
        if(Input.GetKey(KeyCode.D)) horizontalMove += transform.right;
=======
        if (Input.GetKey(KeyCode.W)) horizontalMove += longi;
        if (Input.GetKey(KeyCode.S)) horizontalMove -= longi;
        if (Input.GetKey(KeyCode.A)) horizontalMove -= lateral;
        if (Input.GetKey(KeyCode.D)) horizontalMove += lateral;
>>>>>>> Stashed changes
        horizontalMove = Vector3.ClampMagnitude(horizontalMove, 1f) * SPEED;
    }

    // Retourne le haut du pont si on est dessus, sinon le haut de la planète actuelle
    Vector3 GetUp()
    {
        if (isOnBridge && bridgeUp != Vector3.zero)
        {
            return bridgeUp;
        }
        return (transform.position - planet.position).normalized;
    }

    // Retourne vrai si la marche est possible
    bool IsWalkable(Vector3 surfaceNormal, Vector3 up)
    {
        return Vector3.Angle(surfaceNormal, up) <= maxSlopeAngle;
    }

    // Aligne le joueur avec le sol actuel (planète ou pont)
    void AlignToPlanet(Vector3 up, float dt)
    {
        Quaternion target = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 15f * dt);
    }

    // Bloque les murs et gère les transitions Pont-Planète par l'avant
    Vector3 BlockWalls(Vector3 move, Vector3 up)
    {
        if (move.sqrMagnitude < 1e-7f) return move;

        Vector3 origin = transform.position - up * (sphereRadius * 0.8f);
        float checkDistance = sphereRadius + move.magnitude;

        if (Physics.Raycast(origin, move.normalized, out RaycastHit wall, checkDistance, groundLayer))
        {
            Planet hitPlanet = wall.collider.GetComponentInParent<Planet>();

            // Si l'objet touché devant nous N'EST PAS une planète, c'est le pont 
            if (hitPlanet == null)
            {
                isOnBridge = true;
                bridgeUp = wall.transform.up; // Utilise le "Haut" plat du pont
                return move; // Ne bloque pas le joueur
            }
            // Si on était sur le pont et qu'on touche une planète au bout
            else if (isOnBridge && hitPlanet != null)
            {
                isOnBridge = false;
                planet = hitPlanet.transform; // Bascule la gravité sur la nouvelle planète
                return move;
            }

            if (!IsWalkable(wall.normal, up))
            {
                move = Vector3.ProjectOnPlane(move, wall.normal);
                move = Vector3.ProjectOnPlane(move, up);
            }
        }
        return move;
    }

    void handleVerticalMovement(float step, Vector3 up, float dt)
    {
        // Si on monte (saut), pas besoin de chercher le sol
        if (step > 0f)
        {
            transform.position += up * step;
            isGrounded = false;
            return;
        }

        // Si on descend ou qu'on est au sol : on cherche tous les sols sous nos pieds
        float fall = -step;
        Vector3 origin = transform.position + up * stepHeight;
        float range = stepHeight + sphereRadius + raycastMargin + fall;

        // RaycastAll permet de détecter le pont MÊME s'il chevauche légèrement la surface de la planète
        RaycastHit[] hits = Physics.RaycastAll(origin, -up, range, groundLayer);
        if (hits.Length == 0)
        {
            transform.position += up * step; // rien sous nous : on tombe
            isGrounded = false;
            return;
        }

        // On trie pour prendre le point le plus haut sous nos pieds, en donnant la priorité au pont aux jonctions
        RaycastHit hit = hits[0];
        bool foundBridge = false;

        foreach (RaycastHit h in hits)
        {
            if (h.collider.GetComponentInParent<Planet>() == null) // C'est le pont
            {
                hit = h;
                foundBridge = true;
                break;
            }
            if (h.distance < hit.distance)
            {
                hit = h;
            }
        }

        // Mise à jour de l'état (Pont vs Planète)
        if (foundBridge)
        {
            isOnBridge = true;
            bridgeUp = hit.normal;
            up = bridgeUp;
        }
        else
        {
            Planet hitPlanet = hit.collider.GetComponentInParent<Planet>();
            if (hitPlanet != null)
            {
                isOnBridge = false;
                planet = hitPlanet.transform;
                up = GetUp();
            }
        }

        Vector3 restPosition = hit.point + up * sphereRadius; // position "posé sur le sol"

        if (IsWalkable(hit.normal, up))
        {
            transform.position = restPosition; // on se colle au sol
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

    // Sécurité désactivé sur le pont pour ne pas être téléporté en arrière
    void RecoverIfInsidePlanet()
    {
        if (isOnBridge) return;

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

        // 2. Gravité
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