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
    public const float maxSlopeAngle = 50f;
    public float stepHeight = 0.5f;
    public float slideSpeed = 4f;
    public float minDistanceFromCenter = 5f;
    public LayerMask groundLayer;
    public LayerMask obstacleLayer;   // couches qui bloquent le joueur (Crystal)

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
        groundLayer = LayerMask.GetMask("Planete", "Ground");
    }

    public void handleInput(Transform camera)
    {
        Vector3 up = GetUp();
        Vector3 lateral = Vector3.ProjectOnPlane(camera.right, up).normalized;
        Vector3 longi = Vector3.ProjectOnPlane(camera.forward, up).normalized;

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            verticalSpeed = jumpForce;
            isGrounded = false;
        }
        if (Input.GetKey(KeyCode.W)) horizontalMove += longi;
        if (Input.GetKey(KeyCode.S)) horizontalMove -= longi;
        if (Input.GetKey(KeyCode.A)) horizontalMove -= lateral;
        if (Input.GetKey(KeyCode.D)) horizontalMove += lateral;
        horizontalMove = Vector3.ClampMagnitude(horizontalMove, 1f) * SPEED;
    }

    Vector3 GetUp()
    {
        if (isOnBridge && bridgeUp != Vector3.zero)
        {
            return bridgeUp;
        }
        return (transform.position - planet.position).normalized;
    }

    bool IsWalkable(Vector3 surfaceNormal, Vector3 up)
    {
        return Vector3.Angle(surfaceNormal, up) <= maxSlopeAngle;
    }

    void AlignToPlanet(Vector3 up, float dt)
    {
        Quaternion target = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 15f * dt);
    }

    Vector3 BlockWalls(Vector3 move, Vector3 up)
    {
        if (move.sqrMagnitude < 1e-7f) return move;

        Vector3 origin = transform.position - up * (sphereRadius * 0.8f);
        float checkDistance = sphereRadius + move.magnitude;

        if (Physics.Raycast(origin, move.normalized, out RaycastHit wall, checkDistance, groundLayer))
        {
            Planet hitPlanet = wall.collider.GetComponentInParent<Planet>();

            // FIX 1 : Si on touche le pont de face ou de côté, on NE change PAS isOnBridge ici !
            // On vérifie simplement si la surface touchée est praticable, sinon c'est un mur normal.
            if (isOnBridge && hitPlanet != null)
            {
                // Si on est au bout du pont et qu'on touche la nouvelle planète devant nous
                isOnBridge = false;
                planet = hitPlanet.transform;
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

    // Repousse le joueur hors des obstacles qu'il chevauche (sur le plan tangent à la planète)
    void PushOutOfObstacles(Vector3 up)
    {
        Vector3 center = transform.position;
        Collider[] hits = Physics.OverlapSphere(center, sphereRadius, obstacleLayer, QueryTriggerInteraction.Ignore);

        foreach (Collider col in hits)
        {
            Vector3 closest = col.ClosestPoint(center);
            Vector3 away = center - closest;
            float dist = away.magnitude;

            if (dist < 1e-4f)   // centre du joueur à l'intérieur du collider
            {
                away = center - col.bounds.center;
                dist = 0f;
            }

            away = Vector3.ProjectOnPlane(away, up);   // pas de poussée verticale (on peut rester posé dessus)
            if (away.sqrMagnitude < 1e-8f) continue;

            center += away.normalized * (sphereRadius - dist);
        }

        transform.position = center;
    }   

    void handleVerticalMovement(float step, Vector3 up, float dt)
    {
        if (step > 0f)
        {
            transform.position += up * step;
            isGrounded = false;
            return;
        }

        float fall = -step;
        Vector3 origin = transform.position + up * stepHeight;
        float groundRange = stepHeight + sphereRadius + raycastMargin + fall;

        // Si on est sur le pont, on sonde jusqu'à 10m en dessous pour savoir si on survole toujours le pont pendant un saut
        float searchRange = isOnBridge ? Mathf.Max(groundRange, 10f) : groundRange;

        RaycastHit[] hits = Physics.RaycastAll(origin, -up, searchRange, groundLayer);
        if (hits.Length == 0)
        {
            // Rien même à 10m sous nos pieds : on a sauté dans le vide à côté du pont !
            if (isOnBridge)
            {
                isOnBridge = false;
                FindClosestPlanet();
            }
            transform.position += up * step;
            isGrounded = false;
            return;
        }

        RaycastHit bestHit = new RaycastHit();
        bool foundValidGround = false;
        bool isBridgeHit = false;

        foreach (RaycastHit h in hits)
        {
            bool hitIsBridge = (h.collider.GetComponentInParent<Planet>() == null);

            if (hitIsBridge)
            {
                bool isTopSurface = Vector3.Dot(h.normal, h.transform.up) > 0.8f;
                float heightDiff = stepHeight - h.distance + sphereRadius;
                bool isStepReachable = heightDiff <= (stepHeight * 0.6f);

                if (isTopSurface && (isOnBridge || isStepReachable))
                {
                    bestHit = h;
                    foundValidGround = true;
                    isBridgeHit = true;
                    break;
                }
            }
            else if (h.distance <= groundRange) // Les planètes ne comptent qu'à portée de pied
            {
                if (!foundValidGround || h.distance < bestHit.distance)
                {
                    bestHit = h;
                    foundValidGround = true;
                    isBridgeHit = false;
                }
            }
        }

        if (!foundValidGround)
        {
            // On était sur le pont, mais le rayon n'a plus trouvé le pont sous nous (on est sorti sur le côté)
            if (isOnBridge)
            {
                isOnBridge = false;
                FindClosestPlanet();
            }
            transform.position += up * step;
            isGrounded = false;
            return;
        }

        // Si le pont est détecté loin sous nos pieds (pendant le saut), on garde sa gravité mais on continue de tomber normalement !
        if (bestHit.distance > groundRange)
        {
            transform.position += up * step;
            isGrounded = false;
            return;
        }

        if (isBridgeHit)
        {
            isOnBridge = true;
            bridgeUp = bestHit.transform.up;
            up = bridgeUp;
        }
        else
        {
            Planet hitPlanet = bestHit.collider.GetComponentInParent<Planet>();
            if (hitPlanet != null)
            {
                isOnBridge = false;
                planet = hitPlanet.transform;
                up = GetUp();
            }
        }

        Vector3 restPosition = bestHit.point + up * sphereRadius;

        if (IsWalkable(bestHit.normal, up))
        {
            transform.position = restPosition;
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

            Vector3 downhill = Vector3.ProjectOnPlane(-up, bestHit.normal).normalized;
            transform.position = next + downhill * slideSpeed * dt;
            isGrounded = false;
        }
    }

    // Permet de retomber sur la planète la plus proche si on saute/tombe sur les côtés du pont
    void FindClosestPlanet()
    {
        Planet[] allPlanets = FindObjectsByType<Planet>(FindObjectsSortMode.None);
        float minDistance = Mathf.Infinity;

        foreach (Planet p in allPlanets)
        {
            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                planet = p.transform;
            }
        }
    }

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
        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        Vector3 up = GetUp();
        AlignToPlanet(up, dt);

        // Avancer
        Vector3 move = Vector3.ProjectOnPlane(horizontalMove, up) * dt;
        horizontalMove = Vector3.zero;
        transform.position += BlockWalls(move, up);

        // Gravité
        float previousSpeed = verticalSpeed;
        if (!isGrounded)
            verticalSpeed = Mathf.Max(verticalSpeed - gravity * dt, -maxFallSpeed);
        float verticalStep = (previousSpeed + verticalSpeed) * 0.5f * dt;

        //Monter / descendre / se coller au sol
        up = GetUp();
        handleVerticalMovement(verticalStep, up, dt);
        PushOutOfObstacles(up);

        RecoverIfInsidePlanet();
    }
}