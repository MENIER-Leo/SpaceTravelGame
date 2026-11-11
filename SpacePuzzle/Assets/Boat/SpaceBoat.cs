using UnityEngine;
using UnityEngine.SceneManagement;

public class SpaceBoat : MonoBehaviour
{
    [Header("Navigation Planétaire")]
    public Transform planet;
    public Transform[] waypoints;
    public float boatSpeed = 5f;
    public float turnSpeed = 3f;
    public float heightOffset = 0.5f;
    public float reachThreshold = 2f;
    public LayerMask planetLayer;

    [Header("Scène à charger")]
    public string interiorSceneName = "Bateau_Interieur";

    private int currentIndex = 0;

    void Update()
    {
        if (planet == null || waypoints == null || waypoints.Length == 0) return;

        Transform target = waypoints[currentIndex];
        if (target == null) return;

        // Calcul de la direction du centre
        Vector3 dirFromCenter = (transform.position - planet.position).normalized;

        // On s'oriente vers la cible
        Vector3 dirToTarget = (target.position - transform.position).normalized;
        Vector3 forwardOnSphere = Vector3.ProjectOnPlane(dirToTarget, dirFromCenter).normalized;

        if (forwardOnSphere.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(forwardOnSphere, dirFromCenter);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        transform.position += transform.forward * boatSpeed * Time.deltaTime;

        // LA CORRECTION EST ICI : On recalcule le "bas" depuis la nouvelle position -> IA
        dirFromCenter = (transform.position - planet.position).normalized;

        // On se plaque au sol ou a l'eau
        Vector3 rayStart = planet.position + dirFromCenter * 100f;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, -dirFromCenter, 100f, planetLayer);

        foreach (RaycastHit hit in hits)
        {
            // On ignore le collider du bateau lui-même
            if (hit.transform != transform && !hit.transform.IsChildOf(transform))
            {
                transform.position = hit.point + dirFromCenter * heightOffset;
                break;
            }
        }

        // Vérif de la distance à plat
        Vector3 offsetToTarget = target.position - transform.position;
        Vector3 flatOffset = Vector3.ProjectOnPlane(offsetToTarget, dirFromCenter);

        if (flatOffset.magnitude <= reachThreshold)
        {
            currentIndex = (currentIndex + 1) % waypoints.Length;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Embarquement ! Chargement du bateau intérieur...");
            SceneManager.LoadScene(interiorSceneName);
        }
    }
}