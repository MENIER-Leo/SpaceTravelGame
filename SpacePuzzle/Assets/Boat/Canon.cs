using System.Collections;
using UnityEngine;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Canon du bateau : le joueur s'approche, appuie sur la touche, entre dans le canon et est tiré vers la planète suivante.
// Repères : Bouche_Canon (sortie ; flèche bleue = direction du tir) et Vue_Canon (caméra).
// Menu ⋮ « Créer les repères » pour les générer, puis ajuste-les à la main.
public class Canon : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private KeyCode key = KeyCode.E;
    [SerializeField] private float range = 3f;
    [Tooltip("Vide = l'objet « Joueur ».")]
    [SerializeField] private Transform player;
    [SerializeField] private string inviteLabel = "enter";
    [SerializeField] private float inviteHeight = 2f;
    [SerializeField] private float inviteSize = 4f;

    [Header("Repères")]
    [Tooltip("Sortie du boulet. Flèche bleue (Z) = direction du tir.")]
    [SerializeField] private Transform muzzle;
    [Tooltip("Position de la caméra pendant la séquence.")]
    [SerializeField] private Transform viewPoint;
    [Tooltip("Partie qui pivote pour viser (vide = rien ne pivote).")]
    [SerializeField] private Transform barrel;
    [Tooltip("Degrés dont le fût se relève avant le tir (autour de son axe X rouge).")]
    [SerializeField] private float aimAngle = 12f;

    [Header("Séquence")]
    [SerializeField] private float camFlyTime = 1.2f;
    [SerializeField] private float enterTime = 0.5f;
    [SerializeField] private float aimTime = 1f;
    [SerializeField] private float fuseTime = 1.5f;
    [SerializeField] private float shakeFuse = 0.03f;
    [SerializeField] private float shakeBoom = 0.4f;
    [SerializeField] private float recoil = 0.6f;
    [SerializeField] private float flashIntensity = 40f;
    [SerializeField] private float launchSpeed = 35f;
    [Tooltip("Courbure du vol vers le haut (degrés par seconde).")]
    [SerializeField] private float climbRate = 20f;
    [Tooltip("Durée entre le tir et l'écran entièrement blanc.")]
    [SerializeField] private float flightTime = 1.8f;
    [Tooltip("La caméra suit le joueur après le tir (seulement si rien ne la bloque devant la bouche).")]
    [SerializeField] private bool followCamera = false;
    [SerializeField] private float followDistance = 6f;

    [Header("Transition")]
    [SerializeField] private string nextScene = "Planete3_Base";
    [SerializeField] private Color fadeColor = Color.white;
    [SerializeField] private float fadeOutTime = 1.2f;
    [SerializeField] private float fadeInTime = 1.5f;

    [Header("Sons (optionnels)")]
    [SerializeField] private AudioClip fuseSound;
    [SerializeField] private AudioClip boomSound;

    [Header("Scripts coupés en plus")]
    [Tooltip("PlayerMovement, CameraFollow, PauseMenu et CrystalInteractor sont coupés automatiquement.")]
    [SerializeField] private MonoBehaviour[] disableAlso;

    private TextMeshPro invite;
    private Camera cam;
    private bool used;

    private MonoBehaviour[] controls;
    private Renderer[] renderers;
    private bool[] rendererStates;
    private Collider[] colliders;
    private bool[] colliderStates;
    private Rigidbody rb;
    private bool rbKinematic;
    private CharacterController cc;

    private void Start()
    {
        cam = Camera.main;
        if (player == null)
        {
            GameObject p = GameObject.Find("Joueur");
            if (p != null) player = p.transform;
        }
        if (player == null) Debug.LogWarning($"{name} : joueur introuvable (objet « Joueur » ou champ Player).");
        if (muzzle == null) Debug.LogWarning($"{name} : pas de Bouche_Canon. Menu ⋮ → Créer les repères.");

        GameObject go = new GameObject("Invite_Canon");
        invite = go.AddComponent<TextMeshPro>();
        invite.text = $"{key}\n<size=60%>{inviteLabel}</size>";
        invite.fontSize = inviteSize;
        invite.alignment = TextAlignmentOptions.Center;
        invite.rectTransform.sizeDelta = new Vector2(4f, 2f);
        invite.alpha = 0f;
    }

    private void Update()
    {
        if (used || player == null || invite == null) return;
        bool near = (player.position - transform.position).sqrMagnitude < range * range;
        invite.alpha = Mathf.MoveTowards(invite.alpha, near ? 1f : 0f, 3f * Time.deltaTime);
        if (near && !PauseMenu.IsPaused && Input.GetKeyDown(key)) StartCoroutine(Fire());
    }

    private void LateUpdate()
    {
        if (invite == null) return;
        invite.transform.position = transform.position + transform.up * inviteHeight;
        if (cam != null) invite.transform.rotation = cam.transform.rotation;
    }

    private IEnumerator Fire()
    {
        used = true;
        invite.alpha = 0f;
        bool canLoad = !string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene);
        if (!canLoad) Debug.LogWarning($"{name} : la scène « {nextScene} » n'est pas dans la Scene List (File → Build Profiles). Le tir sera joué sans changer de scène.");

        bool[] states = SetControls(null);
        Transform ct = cam != null ? cam.transform : null;
        Vector3 playerStart = player.position;
        Quaternion playerRot = player.rotation;
        Freeze(true);

        // 1. La caméra se place derrière le canon
        if (ct != null && viewPoint != null) yield return Move(ct, viewPoint.position, viewPoint.rotation, camFlyTime);

        // 2. Le joueur entre dans le canon
        Vector3 inside = muzzle != null ? muzzle.position - muzzle.forward * 0.5f : transform.position;
        yield return Move(player, inside, player.rotation, enterTime);
        Show(false);

        // 3. Le fût se relève
        Quaternion barrelRot0 = barrel != null ? barrel.localRotation : Quaternion.identity;
        if (barrel != null && aimAngle != 0f)
        {
            Quaternion r1 = barrelRot0 * Quaternion.Euler(-aimAngle, 0f, 0f);
            for (float t = 0f; t < aimTime; t += Time.deltaTime)
            {
                barrel.localRotation = Quaternion.Slerp(barrelRot0, r1, Mathf.SmoothStep(0f, 1f, t / aimTime));
                yield return null;
            }
            barrel.localRotation = r1;
        }

        Vector3 dir = muzzle != null ? muzzle.forward : transform.forward;
        Vector3 mouth = muzzle != null ? muzzle.position : transform.position + dir;
        Vector3 up = transform.up;

        // 4. La mèche
        if (fuseSound != null) AudioSource.PlayClipAtPoint(fuseSound, transform.position);
        Light spark = MakeLight("Etincelle", transform.position + up * 0.8f - dir * 0.6f, new Color(1f, 0.6f, 0.2f), 4f);
        Vector3 camBase = ct != null ? ct.position : Vector3.zero;
        for (float t = 0f; t < fuseTime; t += Time.deltaTime)
        {
            spark.intensity = Random.Range(2f, 6f);
            if (ct != null) ct.position = camBase + Random.insideUnitSphere * shakeFuse * (0.3f + t / fuseTime);
            yield return null;
        }
        Destroy(spark.gameObject);
        if (ct != null) ct.position = camBase;

        // 5. Boum
        if (boomSound != null) AudioSource.PlayClipAtPoint(boomSound, mouth);
        Light flash = MakeLight("Flash_Canon", mouth + dir * 0.5f, new Color(1f, 0.85f, 0.6f), 25f);
        FonduScene fade = FonduScene.Create(fadeColor);
        Transform kick = barrel != null ? barrel : transform;
        Vector3 kickBase = kick.position;

        player.SetPositionAndRotation(mouth, Quaternion.LookRotation(dir, up));
        Show(true);
        Vector3 pos = mouth;
        bool fading = false;

        for (float t = 0f; t < flightTime; t += Time.deltaTime)
        {
            float dt = Time.deltaTime;

            // Vol du joueur, qui se redresse vers le haut
            Vector3 axis = Vector3.Cross(dir, up);
            if (axis.sqrMagnitude > 1e-4f && Vector3.Angle(dir, up) > 5f)
                dir = Quaternion.AngleAxis(climbRate * dt, axis.normalized) * dir;
            pos += dir * launchSpeed * dt;
            player.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, up));

            // Recul : coup sec puis retour
            float k = t < 0.08f ? t / 0.08f : Mathf.Max(0f, 1f - (t - 0.08f) / 0.7f);
            kick.position = kickBase - (muzzle != null ? muzzle.forward : transform.forward) * recoil * k;

            flash.intensity = flashIntensity * Mathf.Max(0f, 1f - t / 0.3f);

            // Caméra : secousse, et éventuellement poursuite
            if (ct != null)
            {
                Vector3 shake = Random.insideUnitSphere * shakeBoom * Mathf.Max(0f, 1f - t / 0.6f);
                if (followCamera)
                {
                    Vector3 chase = pos - dir * followDistance + up * 2f;
                    ct.position = Vector3.Lerp(camBase, chase, Mathf.SmoothStep(0f, 1f, t / 0.8f)) + shake;
                    ct.rotation = Quaternion.Slerp(ct.rotation, Quaternion.LookRotation(pos - ct.position, up), 10f * dt);
                }
                else ct.position = camBase + shake;
            }

            if (!fading && t >= flightTime - fadeOutTime)
            {
                fading = true;
                StartCoroutine(fade.FadeTo(1f, fadeOutTime));
            }
            yield return null;
        }
        kick.position = kickBase;
        Destroy(flash.gameObject);
        while (fade.Alpha < 0.999f) yield return null;

        // 6. Planète suivante
        if (canLoad)
        {
            fade.LoadAndReveal(nextScene, fadeInTime);
            yield break;
        }

        // Scène absente : on remet tout en place pour pouvoir retester
        yield return new WaitForSeconds(0.5f);
        player.SetPositionAndRotation(playerStart, playerRot);
        Freeze(false);
        if (barrel != null) barrel.localRotation = barrelRot0;
        SetControls(states);
        fade.Reveal(fadeInTime);
        used = false;
    }

    // ---------- Outils ----------

    private static IEnumerator Move(Transform t, Vector3 pos, Quaternion rot, float time)
    {
        Vector3 p = t.position;
        Quaternion r = t.rotation;
        for (float x = 0f; x < time; x += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, x / time);
            t.SetPositionAndRotation(Vector3.Lerp(p, pos, k), Quaternion.Slerp(r, rot, k));
            yield return null;
        }
        t.SetPositionAndRotation(pos, rot);
    }

    private static Light MakeLight(string objName, Vector3 pos, Color color, float range)
    {
        Light l = new GameObject(objName).AddComponent<Light>();
        l.transform.position = pos;
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = 0f;
        return l;
    }

    // restore == null : coupe les contrôles et renvoie leur état ; sinon : le restaure.
    private bool[] SetControls(bool[] restore)
    {
        if (controls == null)
        {
            var list = new System.Collections.Generic.List<MonoBehaviour>
            {
                FindFirstObjectByType<PlayerMovement>(),
                FindFirstObjectByType<CameraFollow>(),
                FindFirstObjectByType<PauseMenu>(),
                FindFirstObjectByType<CrystalInteractor>()
            };
            if (disableAlso != null) list.AddRange(disableAlso);
            controls = list.ToArray();
        }

        if (restore == null)
        {
            bool[] s = new bool[controls.Length];
            for (int i = 0; i < controls.Length; i++)
            {
                if (controls[i] == null) continue;
                s[i] = controls[i].enabled;
                controls[i].enabled = false;
            }
            return s;
        }
        for (int i = 0; i < controls.Length; i++)
            if (controls[i] != null) controls[i].enabled = restore[i];
        return null;
    }

    // Fige le joueur : plus de physique ni de collisions pendant la séquence.
    private void Freeze(bool freeze)
    {
        if (freeze)
        {
            renderers = player.GetComponentsInChildren<Renderer>(true);
            rendererStates = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) rendererStates[i] = renderers[i].enabled;

            colliders = player.GetComponentsInChildren<Collider>(true);
            colliderStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                colliderStates[i] = colliders[i].enabled;
                colliders[i].enabled = false;
            }

            rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rbKinematic = rb.isKinematic;
                if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }
        else
        {
            Show(true);
            for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
            if (rb != null) rb.isKinematic = rbKinematic;
            if (cc != null) cc.enabled = true;
        }
    }

    private void Show(bool show)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = show && rendererStates[i];
    }

    private void OnDestroy()
    {
        if (invite != null) Destroy(invite.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, range);
        if (muzzle != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(muzzle.position, muzzle.position + muzzle.forward * 4f);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Créer les repères")]
    private void CreateMarkers()
    {
        Renderer[] rs = GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(transform.position, Vector3.one);
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);

        Vector3 fwd = transform.forward, up = transform.up;
        float extF = Mathf.Abs(b.extents.x * fwd.x) + Mathf.Abs(b.extents.y * fwd.y) + Mathf.Abs(b.extents.z * fwd.z);
        float extU = Mathf.Abs(b.extents.x * up.x) + Mathf.Abs(b.extents.y * up.y) + Mathf.Abs(b.extents.z * up.z);

        Undo.RecordObject(this, "Repères du canon");

        if (muzzle == null)
        {
            GameObject m = new GameObject("Bouche_Canon");
            Undo.RegisterCreatedObjectUndo(m, "Bouche_Canon");
            m.transform.SetParent(barrel != null ? barrel : transform, false);
            m.transform.SetPositionAndRotation(b.center + fwd * extF, Quaternion.LookRotation(fwd, up));
            muzzle = m.transform;
        }

        if (viewPoint == null)
        {
            GameObject v = new GameObject("Vue_Canon");
            Undo.RegisterCreatedObjectUndo(v, "Vue_Canon");
            v.transform.SetParent(transform, false);
            Vector3 p = b.center - fwd * (extF + 4f) + up * (extU + 1.5f) + transform.right * 1.5f;
            v.transform.SetPositionAndRotation(p, Quaternion.LookRotation(muzzle.position - p, up));
            viewPoint = v.transform;
        }

        EditorUtility.SetDirty(this);
        Debug.Log($"{name} : repères créés. Vérifie que la flèche bleue de Bouche_Canon sort bien du fût.");
    }
#endif
}
