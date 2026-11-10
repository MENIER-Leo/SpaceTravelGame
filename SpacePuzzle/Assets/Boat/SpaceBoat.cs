using UnityEngine;
using UnityEngine.SceneManagement;

public class SpaceBoat : MonoBehaviour
{
    [Header("Navigation Planétaire")]
    public Transform planet;
    public float boatSpeed = 15f;
    public float heightOffset = 0.5f;
    public LayerMask planetLayer;

    [Header("Scène à charger")]
    public string interiorSceneName = "Bateau_Interieur";

    void Update()
    {
        if (planet == null) return;

        // 1. Avancer autour de la planète
        transform.RotateAround(planet.position, transform.right, boatSpeed * Time.deltaTime);
        Vector3 dirFromCenter = (transform.position - planet.position).normalized;

        // 2. Coller le bateau à la surface (relief/eau)
        Vector3 rayStart = planet.position + dirFromCenter * 100f;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, -dirFromCenter, 100f, planetLayer);

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform != transform && !hit.transform.IsChildOf(transform))
            {
                transform.position = hit.point + dirFromCenter * heightOffset;
                break;
            }
        }

        // 3. Aligner l'inclinaison du bateau avec la courbure
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, dirFromCenter) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 15f * Time.deltaTime);
    }

    // Détection du joueur
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Embarquement ! Chargement du bateau intérieur...");
            SceneManager.LoadScene(interiorSceneName);
        }
    }
}