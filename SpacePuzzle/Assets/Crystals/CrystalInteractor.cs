using UnityEngine;

public class CrystalInteractor : MonoBehaviour
{
    [SerializeField] private float range = 2.5f;
    [SerializeField] private LayerMask crystalMask;
    [SerializeField] private KeyCode rotateLeftKey = KeyCode.Q;
    [SerializeField] private KeyCode rotateRightKey = KeyCode.E;

    private Crystal current;
    private KeyCode heldKey = KeyCode.None;

    private void Update()
    {
        UpdateTarget();
        HandleInput();
    }

    private void UpdateTarget()
    {
        Crystal best = null;
        float bestDist = float.MaxValue;

        Collider[] hits = Physics.OverlapSphere(transform.position, range, crystalMask, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
        {
            Crystal c = hit.GetComponentInParent<Crystal>();
            if (c == null || !c.rotatable) continue;

            float d = (c.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = c; }
        }

        if (best == current) return;

        if (current != null)
        {
            current.StopTurning();
            current.SetHighlighted(false);
        }

        current = best;
        heldKey = KeyCode.None;

        if (current != null) current.SetHighlighted(true);
    }

    private void HandleInput()
    {
        if (current == null) return;

        if (Input.GetKeyDown(rotateLeftKey)) { heldKey = rotateLeftKey; current.StartTurning(-1); }
        else if (Input.GetKeyDown(rotateRightKey)) { heldKey = rotateRightKey; current.StartTurning(1); }

        if (heldKey != KeyCode.None && Input.GetKeyUp(heldKey))
        {
            heldKey = KeyCode.None;
            current.StopTurning();
        }
    }

    private void OnDisable()
    {
        if (current != null)
        {
            current.StopTurning();
            current.SetHighlighted(false);
            current = null;
        }
        heldKey = KeyCode.None;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
