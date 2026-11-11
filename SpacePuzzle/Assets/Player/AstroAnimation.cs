using UnityEngine;

public class AstroAnimation : MonoBehaviour
{
    private Animator animator;
    public float turnSpeed = 15f;

    // On ajoute une référence à la caméra
    private Transform mainCamera;

    void Start()
    {
        animator = GetComponent<Animator>();
        // On trouve automatiquement la caméra principale de ta scène
        mainCamera = Camera.main.transform;
    }

    void Update()
    {
        float moveHorizontal = Input.GetAxisRaw("Horizontal");
        float moveVertical = Input.GetAxisRaw("Vertical");

        // On récupère la direction de la caméra, projetée sur le sol (sphérique) du joueur
        Vector3 camForward = Vector3.ProjectOnPlane(mainCamera.forward, transform.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.right, transform.up).normalized;

        // On calcule la vraie direction finale en combinant la caméra et les touches
        Vector3 direction = (camForward * moveVertical + camRight * moveHorizontal).normalized;

        bool isMoving = direction.magnitude > 0.1f;
        animator.SetBool("isRunning", isMoving);

        // On fait pivoter le modèle vers cette nouvelle direction
        if (isMoving)
        {
            // On calcule la rotation pour regarder dans la direction du mouvement, 
            // tout en gardant la tête vers le ciel de la planète (transform.up)
            Quaternion targetRotation = Quaternion.LookRotation(direction, transform.up);

            // On tourne le modèle de façon fluide
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }
}