using UnityEngine;

public class CameraFollow : MonoBehaviour
{
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

    void LateUpdate()
    {
        if (player != null)
        {
            if (Input.GetMouseButton(1))
            {
                currentX -= Input.GetAxis("Mouse X") * sensitivityX;
                currentY -= Input.GetAxis("Mouse Y") * sensitivityY;
                currentY = Mathf.Clamp(currentY, minY, maxY);
            }

            //Créer la rotation de la caméra à partir des angles de la souris
            Quaternion localOrbit = Quaternion.Euler(currentY, currentX, 0f);

            //Combiner l'inclinaison du joueur sur la planète avec la rotation de la souris
            Quaternion finalRotation = player.rotation * localOrbit;

            //Calculer le recul de la caméra selon cette rotation finale
            Vector3 direction = new Vector3(0f, 0f, -distance);
            transform.position = player.position + (finalRotation * direction);

            //Orienter la caméra vers le joueur (en gardant la tête à l'endroit sur la sphère)
            transform.rotation = finalRotation;
        }
    }
}