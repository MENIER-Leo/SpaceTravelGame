using UnityEngine;

public class CrystalInteractor : MonoBehaviour
{
    [SerializeField] private float range = 2.5f;
    [SerializeField] private LayerMask crystalMask;
    [SerializeField] private KeyCode rotateLeftKey = KeyCode.Q;
    [SerializeField] private KeyCode rotateRightKey = KeyCode.E;
    [SerializeField] private KeyCode resetHoldKey = KeyCode.E;

    private MonoBehaviour current;   // Crystal ou ResetCrystal
    private KeyCode heldKey = KeyCode.None;

    private void Update()
    {
        UpdateTarget();
        HandleInput();
    }

    private void UpdateTarget()
    {
        MonoBehaviour best = null;
        float bestDist = float.MaxValue;

        Collider[] hits = Physics.OverlapSphere(transform.position, range, crystalMask, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
        {
            MonoBehaviour candidate = null;

            Crystal c = hit.GetComponentInParent<Crystal>();
            if (c != null)
            {
                if (c.rotatable && !c.IsAbsorbed) candidate = c;
            }
            else
            {
                ResetCrystal r = hit.GetComponentInParent<ResetCrystal>();
                if (r != null && r.CanInteract) candidate = r;
            }

            if (candidate == null) continue;

            float d = (candidate.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = candidate; }
        }

        if (best == current) return;

        Release(current);
        current = best;
        heldKey = KeyCode.None;

        if (current is Crystal crystal) crystal.SetHighlighted(true);
        else if (current is ResetCrystal reset) reset.SetHighlighted(true);
    }

    private void HandleInput()
    {
        if (current is Crystal crystal)
        {
            if (Input.GetKeyDown(rotateLeftKey)) { heldKey = rotateLeftKey; crystal.StartTurning(-1); }
            else if (Input.GetKeyDown(rotateRightKey)) { heldKey = rotateRightKey; crystal.StartTurning(1); }

            if (heldKey != KeyCode.None && Input.GetKeyUp(heldKey))
            {
                heldKey = KeyCode.None;
                crystal.StopTurning();
            }
        }
        else if (current is ResetCrystal reset)
        {
            if (Input.GetKeyDown(resetHoldKey)) { heldKey = resetHoldKey; reset.BeginHold(); }

            if (heldKey == resetHoldKey && Input.GetKeyUp(resetHoldKey))
            {
                heldKey = KeyCode.None;
                reset.EndHold();
            }
        }
    }

    private void Release(MonoBehaviour target)
    {
        if (target is Crystal c)
        {
            c.StopTurning();
            c.SetHighlighted(false);
        }
        else if (target is ResetCrystal r)
        {
            r.EndHold();
            r.SetHighlighted(false);
        }
    }

    private void OnDisable()
    {
        Release(current);
        current = null;
        heldKey = KeyCode.None;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
