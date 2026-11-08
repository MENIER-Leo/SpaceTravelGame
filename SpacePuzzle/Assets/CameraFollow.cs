using UnityEngine;

public class CameraFollow : MonoBehaviour
{

    [Header("Collision caméra")]
    public LayerMask collisionMask;         // couches qui bloquent la caméra (Ground)
    public float collisionRadius = 0.3f;    // marge autour du rayon pour ne pas frôler les murs
    public float minDistance = 1.0f;        // distance minimale au joueur
    public float zoomOutSpeed = 8f;         // vitesse de recul quand le mur n'est plus là

    private float currentDistance;
    public Transform player;
    public float distance = 10.0f;

    [Header("Sensibilité de la souris")]
    public float sensitivityX = 4.0f;
    public float sensitivityY = 2.0f;

    [Header("Limites d'angle vertical")]
    public float minY = 5.0f;  // Bloqué à 5 degrés pour ne pas traverser le sol de la planète
    public float maxY = 80.0f;

    private float currentX = 0.0f;
    private float currentY = 20.0f;

    void Start()
    {
        player = GameObject.Find("Joueur").transform;
        currentDistance = distance;
    }

    void LateUpdate()
    {
        if (player != null)
        {
            if (Input.GetMouseButton(1))
            {
                currentX += Input.GetAxis("Mouse X") * sensitivityX;
                currentY -= Input.GetAxis("Mouse Y") * sensitivityY;
                currentY = Mathf.Clamp(currentY, minY, maxY);
            }

            //Crée la rotation de la caméra à partir des angles de la souris
            Quaternion localOrbit = Quaternion.Euler(currentY, currentX, 0f);

            //Combine l'inclinaison du joueur sur la planète avec la rotation de la souris
            Quaternion finalRotation = player.rotation * localOrbit;

            //Calcule le recul de la caméra selon la rotation
            Vector3 back = finalRotation * Vector3.back;
            float wanted = distance;

            if (Physics.SphereCast(player.position, collisionRadius, back, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(hit.distance, minDistance);

            // Se rapproche immédiatement d'un mur, recule en douceur quand il disparaît
            currentDistance = wanted < currentDistance ? wanted : Mathf.MoveTowards(currentDistance, wanted, zoomOutSpeed * Time.deltaTime);

            transform.position = player.position + back * currentDistance;

            //Oriente la caméra vers le joueur (en gardant la tête à l'endroit)
            transform.rotation = finalRotation;
        }
    }
}