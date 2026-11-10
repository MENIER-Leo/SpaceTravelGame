using UnityEngine;
using TMPro;

public class CrystalInteractor : MonoBehaviour
{
    [SerializeField] private float range = 2.5f;
    [SerializeField] private LayerMask crystalMask;
    [SerializeField] private KeyCode rotateLeftKey = KeyCode.Q;
    [SerializeField] private KeyCode rotateRightKey = KeyCode.E;
    [SerializeField] private KeyCode resetHoldKey = KeyCode.E;

    [Header("Invite")]
    public GameObject invite;
    public TMP_Text inviteText;
    [SerializeField] private CanvasGroup inviteGroup;
    [SerializeField] private float idleDelay = 3f;
    [SerializeField] private float inviteHeight = 1.5f;
    [SerializeField, Range(0f, 1f)] private float inviteAlpha = 0.55f;
    [SerializeField] private float fadeSpeed = 2f;

    private float idleTimer;
    private Camera cam;

    private static KeyCode[] keyboardKeys;


    private MonoBehaviour current;   // Crystal ou ResetCrystal
    private KeyCode heldKey = KeyCode.None;

    private void Update()
    {
        UpdateTarget();
        HandleInput();
        UpdateInviteFade();
    }

    private void Start()
    {
        cam = Camera.main;
        if (inviteGroup != null) inviteGroup.alpha = 0f;
        if (keyboardKeys == null)
            keyboardKeys = System.Array.FindAll((KeyCode[])System.Enum.GetValues(typeof(KeyCode)), k => k < KeyCode.Mouse0);
    }

    private void LateUpdate()
    {
        if (invite == null) return;
        if (current != null)
            invite.transform.position = current.transform.position + current.transform.up * inviteHeight;
        if (cam != null)
            invite.transform.rotation = cam.transform.rotation;
    }

    private void UpdateInviteFade()
    {
        if (inviteGroup == null) return;

        if (AnyKeyboardKey()) idleTimer = 0f;
        else idleTimer += Time.deltaTime;

        float target = (current != null && idleTimer >= idleDelay) ? inviteAlpha : 0f;
        inviteGroup.alpha = Mathf.MoveTowards(inviteGroup.alpha, target, fadeSpeed * Time.deltaTime);
    }

    private bool AnyKeyboardKey()
    {
        if (!Input.anyKey) return false;
        foreach (KeyCode k in keyboardKeys)
            if (Input.GetKey(k)) return true;
        return false;
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
        UpdateInvite();
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
        if (inviteGroup != null) inviteGroup.alpha = 0f;
    }

    private void UpdateInvite()
    {
        if (inviteText == null || current == null) return;

        if (current is Crystal)
            inviteText.text = $"{rotateLeftKey}   {rotateRightKey}\n<size=60%>turn</size>";
        else if (current is ResetCrystal)
            inviteText.text = $"{resetHoldKey}\n<size=60%>maintain</size>";
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
