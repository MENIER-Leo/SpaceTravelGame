using UnityEngine;

public class SpacePirate : MonoBehaviour
{
    public Transform player;
    public Transform respawnPoint;
    public Transform planet;

    public Transform[] waypoints;
    public float walkSpeed = 4f;
    public float turnSpeed = 10f;
    public float stopDistance = 0.2f;

    public float viewDistance = 8f;
    public float viewAngle = 45f;
    public LayerMask obstacleLayer;

    private int currentPoint = 0;
    private int direction = 1;
    private bool playerInSight = false;

    void Start()
    {
        if (planet != null)
        {
            Vector3 up = (transform.position - planet.position).normalized;
            transform.rotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
        }
    }

    void Update()
    {
        if (player == null) return;

        if (waypoints != null && waypoints.Length > 0)
        {
            PatrolInBoat();
        }

        CheckLineOfSight();
    }

    void PatrolInBoat()
    {
        Transform target = waypoints[currentPoint];
        if (target == null) return;

        Vector3 targetPos = new Vector3(target.position.x, transform.position.y, target.position.z);
        Vector3 moveDir = targetPos - transform.position;

        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(moveDir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, turnSpeed * Time.deltaTime);
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPos, walkSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) <= stopDistance && waypoints.Length > 1)
        {
            if (currentPoint == waypoints.Length - 1)
            {
                direction = -1;
            }
            else if (currentPoint == 0)
            {
                direction = 1;
            }

            currentPoint += direction;
        }
    }

    void CheckLineOfSight()
    {
        bool canSee = false;
        Vector3 dirToPlayer = player.position - transform.position;
        float distance = dirToPlayer.magnitude;

        if (distance <= viewDistance)
        {
            Vector3 flatDir = Vector3.ProjectOnPlane(dirToPlayer, transform.up).normalized;
            float angle = Vector3.Angle(transform.forward, flatDir);

            if (angle <= viewAngle * 0.5f)
            {
                Vector3 eyePos = transform.position + transform.up * 0.5f;
                if (!Physics.Raycast(eyePos, dirToPlayer.normalized, distance, obstacleLayer))
                {
                    canSee = true;
                }
            }
        }

        if (canSee != playerInSight)
        {
            playerInSight = canSee;
            if (playerInSight)
            {
                Debug.Log("Le pirate voit le joueur");
            }
            else
            {
                Debug.Log("Le pirate ne voit plus le joueur");
            }
        }

        if (canSee)
        {
            CatchPlayer();
        }
    }

    void CatchPlayer()
    {
        if (respawnPoint != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.position = respawnPoint.position;

            if (cc != null) cc.enabled = true;
        }
    }
}