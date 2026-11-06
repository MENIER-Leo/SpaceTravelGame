using UnityEngine;

public class SpacePirate : MonoBehaviour
{
    [Header("Références")]
    public Transform planet;
    public Transform player;
    public Transform respawnPoint;

    [Header("Patrouille / Rotation")]
    public float patrolSpeed = 10f;
    public float turnSpeed = 20f;
    public float heightOffset = 1f;
    public bool walkAroundPlanet = true;
    [Tooltip("Coche si le pirate ne doit marcher QUE sur l'objet 'planet' et ignorer le pont")]
    public bool stickOnlyToPlanet = true;

    [Header("Cône de Vision")]
    public float viewDistance = 8f;
    public float viewAngle = 45f;
    public LayerMask obstacleLayer;

    void Update()
    {
        if (planet == null || player == null) return;

        Vector3 dirFromCenter = (transform.position - planet.position).normalized;

        if (walkAroundPlanet)
        {
            transform.RotateAround(planet.position, transform.right, patrolSpeed * Time.deltaTime);
            dirFromCenter = (transform.position - planet.position).normalized;
        }
        else
        {
            transform.Rotate(dirFromCenter, turnSpeed * Time.deltaTime, Space.World);
        }

        // 1. On fait partir le rayon juste au-dessus du pirate (5m) et non à 100m dans l'espace
        float rayHeight = 5f;
        float maxRayDistance = 20f;
        Vector3 rayStart = transform.position + dirFromCenter * rayHeight;

        RaycastHit[] hits = Physics.RaycastAll(rayStart, -dirFromCenter, maxRayDistance, obstacleLayer);

        // 2. On cherche l'impact valide LE PLUS PROCHE du pirate
        RaycastHit closestHit = new RaycastHit();
        float closestDistance = Mathf.Infinity;
        bool foundGround = false;

        foreach (RaycastHit hit in hits)
        {
            // Ignore le pirate lui-même
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            // Si activé, ignore le pont et les autres planètes
            if (stickOnlyToPlanet && hit.transform != planet && !hit.transform.IsChildOf(planet))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                foundGround = true;
            }
        }

        if (foundGround)
        {
            transform.position = closestHit.point + dirFromCenter * heightOffset;
        }

        Quaternion targetRot = Quaternion.FromToRotation(transform.up, dirFromCenter) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 15f * Time.deltaTime);

        CheckLineOfSight();
    }

    void CheckLineOfSight()
    {
        Vector3 dirToPlayer = player.position - transform.position;
        float distanceToPlayer = dirToPlayer.magnitude;

        if (distanceToPlayer <= viewDistance)
        {
            float angle = Vector3.Angle(transform.forward, dirToPlayer.normalized);
            if (angle <= viewAngle * 0.5f)
            {
                Vector3 eyePosition = transform.position + transform.up * 0.5f;
                if (!Physics.Raycast(eyePosition, dirToPlayer.normalized, distanceToPlayer, obstacleLayer))
                {
                    CatchPlayer();
                }
            }
        }
    }

    void CatchPlayer()
    {
        Debug.Log("Repéré par les pirates !");
        if (respawnPoint != null)
        {
            player.position = respawnPoint.position;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        Vector3 leftRay = Quaternion.AngleAxis(-viewAngle * 0.5f, transform.up) * transform.forward;
        Vector3 rightRay = Quaternion.AngleAxis(viewAngle * 0.5f, transform.up) * transform.forward;

        Gizmos.DrawRay(transform.position, leftRay * viewDistance);
        Gizmos.DrawRay(transform.position, rightRay * viewDistance);
    }
}