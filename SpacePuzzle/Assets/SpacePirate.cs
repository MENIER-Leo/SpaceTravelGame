using UnityEngine;

public class SpacePirate : MonoBehaviour
{
    [Header("Références")]
    public Transform planet;
    public Transform player;
    public Transform respawnPoint;

    [Header("Patrouille / Rotation")]
    public float patrolSpeed = 25f;
    public float turnSpeed = 40f;
    public bool walkAroundPlanet = true;

    [Header("Cône de Vision")]
    public float viewDistance = 8f;
    public float viewAngle = 45f;
    public LayerMask obstacleLayer;

    void Update()
    {
        if (planet == null || player == null) return;

        // Maintenir le pirate debout sur la courbure de la planète
        Vector3 up = (transform.position - planet.position).normalized;

        if (walkAroundPlanet)
        {
            // Fait avancer le pirate en cercle autour de la planète
            transform.RotateAround(planet.position, transform.right, patrolSpeed * Time.deltaTime);
        }
        else
        {
            // Mode sentinelle fixe : tourne sur lui-même pour scanner les alentours
            transform.Rotate(up, turnSpeed * Time.deltaTime, Space.World);
        }

        // Réaligne ses pieds avec la normale de la planète
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);

        // Vérifier si le joueur est repéré
        CheckLineOfSight();
    }

    void CheckLineOfSight()
    {
        Vector3 dirToPlayer = player.position - transform.position;
        float distanceToPlayer = dirToPlayer.magnitude;

        if (distanceToPlayer <= viewDistance)
        {
            // Le joueur est-il dans l'angle de vue ?
            float angle = Vector3.Angle(transform.forward, dirToPlayer.normalized);
            if (angle <= viewAngle * 0.5f)
            {
                // Y a-t-il un obstacle entre les deux ?
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
            // Téléporte le joueur au point de départ de la planète 2
            player.position = respawnPoint.position;
        }
    }

    // Dessine le cône de vision en rouge dans la vue Scene pour aider à régler la difficulté
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