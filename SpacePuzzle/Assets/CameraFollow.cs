using UnityEngine;

public class CameraFollowTrig : MonoBehaviour
{
    public Transform player;
    public float distance = 10.0f;

    [Header("Sensibilité de la souris")]
    public float sensitivityX = 4.0f;
    public float sensitivityY = 2.0f;

    [Header("Limites d'angle vertical")]
    public float minY = -20.0f;
    public float maxY = 80.0f;

    private float currentX = 0.0f;
    private float currentY = 0.0f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        currentX = angles.y;
        currentY = angles.x;
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

            // Conversion des angles en radians pour la trigonométrie
            float pitch = currentY * Mathf.Deg2Rad;
            float yaw = currentX * Mathf.Deg2Rad;

            // Formule des coordonnées sphériques pour un espace 3D (Y vers le haut)
            float offsetX = distance * Mathf.Sin(yaw) * Mathf.Cos(pitch);
            float offsetY = distance * Mathf.Sin(pitch);
            float offsetZ = -distance * Mathf.Cos(yaw) * Mathf.Cos(pitch);

            // On applique l'offset calculé à la position du joueur
            transform.position = player.position + new Vector3(offsetX, offsetY, offsetZ);

            transform.LookAt(player);
        }
    }
}