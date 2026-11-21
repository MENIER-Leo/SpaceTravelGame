using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Look des PNJ : petite boule noire hérissée de piques, yeux lumineux.
/// Clic droit sur le composant > "Construire le PNJ". La capsule garde sa collision.
public class NpcLook : MonoBehaviour
{
    public enum Accessoire { Aucun, ChapeauPirate }

    [Header("Construction")]
    [SerializeField, Tooltip("Capsule d'origine. Vide = enfant \"Capsule\".")]
    private Transform capsule;
    [SerializeField, Tooltip("Rayon de la boule, en fraction de la hauteur de la capsule.")]
    private float taille = 0.25f;
    [SerializeField] private int nombrePiques = 40;
    [SerializeField] private Vector2 longueurPiques = new Vector2(0.35f, 0.65f);
    [SerializeField] private int graine = 7;
    [SerializeField] private Accessoire accessoire = Accessoire.Aucun;

    [Header("Couleurs")]
    [SerializeField] private Color couleurCorps = new Color(0.04f, 0.03f, 0.06f);
    [SerializeField] private Color couleurPiques = new Color(0.12f, 0.05f, 0.18f);
    [SerializeField] private Color couleurYeux = new Color(0.9f, 0.85f, 1f);
    [SerializeField] private float lueurYeux = 1.5f;

    [Header("Animation")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float lookRange = 6f;
    [SerializeField] private float turnSpeed = 4f;
    [SerializeField] private float breathAmount = 0.04f;
    [SerializeField] private float breathSpeed = 0.5f;
    [SerializeField] private float floatAmount = 0.05f;
    [SerializeField] private float floatSpeed = 0.4f;
    [SerializeField] private Vector2 blinkInterval = new Vector2(2.5f, 5f);

    [SerializeField, HideInInspector] private Transform corps;
    [SerializeField, HideInInspector] private Transform[] yeux;
    [SerializeField, HideInInspector] private float rayon = 0.5f;

    private Transform player;
    private Vector3 corpsScale, corpsPos;
    private Vector3[] yeuxScale;
    private float yaw, phase;

    private void Start()
    {
        phase = Random.value * 10f;
        GameObject p = GameObject.FindWithTag(playerTag);
        if (p != null) player = p.transform;

        if (corps != null) { corpsScale = corps.localScale; corpsPos = corps.localPosition; }
        if (yeux != null && yeux.Length > 0)
        {
            yeuxScale = new Vector3[yeux.Length];
            for (int i = 0; i < yeux.Length; i++) yeuxScale[i] = yeux[i].localScale;
            StartCoroutine(Blink());
        }
    }

    private void Update()
    {
        if (corps == null) return;
        float t = Time.time + phase;
        float b = Mathf.Sin(t * breathSpeed * Mathf.PI * 2f) * breathAmount;
        float f = Mathf.Sin(t * floatSpeed * Mathf.PI * 2f) * floatAmount * rayon * 2f;

        corps.localScale = new Vector3(corpsScale.x * (1f - b * 0.5f), corpsScale.y * (1f + b), corpsScale.z * (1f - b * 0.5f));
        corps.localPosition = corpsPos + Vector3.up * f;

        float target = yaw;
        if (player != null)
        {
            Vector3 local = corps.parent.InverseTransformPoint(player.position);
            if (local.magnitude < lookRange)
                target = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        }
        yaw = Mathf.LerpAngle(yaw, target, Time.deltaTime * turnSpeed);
        corps.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private IEnumerator Blink()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(blinkInterval.x, blinkInterval.y));
            for (float t = 0f; t < 0.16f; t += Time.deltaTime)
            {
                float k = 1f - Mathf.Sin(t / 0.16f * Mathf.PI) * 0.9f;
                for (int i = 0; i < yeux.Length; i++)
                    yeux[i].localScale = new Vector3(yeuxScale[i].x, yeuxScale[i].y * k, yeuxScale[i].z);
                yield return null;
            }
            for (int i = 0; i < yeux.Length; i++) yeux[i].localScale = yeuxScale[i];
        }
    }

#if UNITY_EDITOR
    private const string MatFolder = "Assets/Npc/Materials";
    private const string MeshFolder = "Assets/Npc/Meshes";

    [ContextMenu("Construire le PNJ")]
    private void Build()
    {
        if (capsule == null) capsule = transform.Find("Capsule");
        if (capsule == null) { Debug.LogError($"{name} : Capsule introuvable."); return; }

        Undo.RecordObject(this, "Construire PNJ");
        Transform old = transform.Find("Corps_PNJ");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // Taille de référence = capsule Unity (2 de haut)
        float H = capsule.localScale.y * 2f;
        float bottom = capsule.localPosition.y - capsule.localScale.y;
        float R = taille * H;
        rayon = R;

        var rootGO = new GameObject("Corps_PNJ");
        Undo.RegisterCreatedObjectUndo(rootGO, "Construire PNJ");
        Transform root = rootGO.transform;
        root.SetParent(transform, false);
        root.localPosition = new Vector3(0f, bottom, 0f);
        rootGO.layer = gameObject.layer;

        Renderer capR = capsule.GetComponent<Renderer>();
        if (capR != null) { Undo.RecordObject(capR, "Construire PNJ"); capR.enabled = false; }

        Material mCorps = Mat("Boule_Corps", couleurCorps, 0.1f, 0f);
        Material mPiques = Mat("Boule_Piques", couleurPiques, 0.3f, 0f);
        Material mYeux = Mat("Boule_Yeux", couleurYeux, 0.7f, lueurYeux);
        Material mNoir = Mat("Boule_Pupille", new Color(0.02f, 0.02f, 0.03f), 0.9f, 0f);
        Mesh pique = SpikeMesh();

        // --- Pieds (immobiles) ---
        for (int i = 0; i < 2; i++)
        {
            float x = (i == 0 ? -1f : 1f) * 0.4f * R;
            Part(PrimitiveType.Sphere, i == 0 ? "Pied_G" : "Pied_D", root, new Vector3(x, 0.08f * R, 0.1f * R),
                 Vector3.zero, new Vector3(0.35f * R, 0.18f * R, 0.45f * R), mCorps);
        }

        // --- Boule (respire, flotte, se tourne) ---
        corps = Empty("Corps", root, new Vector3(0f, R * 1.1f, 0f));
        Part(PrimitiveType.Sphere, "Boule", corps, Vector3.zero, Vector3.zero, Vector3.one * 2f * R, mCorps);

        // Piques réparties sur la sphère (Fibonacci), sauf devant les yeux et dessous
        var rng = new System.Random(graine);
        Vector3 face = new Vector3(0f, 0.25f, 1f).normalized;
        for (int i = 0; i < nombrePiques; i++)
        {
            float y = 1f - 2f * (i + 0.5f) / nombrePiques;
            float r = Mathf.Sqrt(1f - y * y);
            float phi = i * 2.39996f;
            Vector3 dir = new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r);

            if (Vector3.Dot(dir, face) > 0.7f) continue;          // dégage le visage
            if (dir.y < -0.55f) continue;                          // rien sous la boule
            if (accessoire == Accessoire.ChapeauPirate && dir.y > 0.7f) continue;

            float len = Mathf.Lerp(longueurPiques.x, longueurPiques.y, (float)rng.NextDouble()) * R;
            float w = 0.13f * R;
            GameObject s = new GameObject("Pique");
            s.layer = gameObject.layer;
            s.transform.SetParent(corps, false);
            s.transform.localPosition = dir * R * 0.85f;
            s.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            s.transform.localScale = new Vector3(w, len, w);
            s.AddComponent<MeshFilter>().sharedMesh = pique;
            s.AddComponent<MeshRenderer>().sharedMaterial = mPiques;
        }

        // Yeux lumineux
        yeux = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float sx = i == 0 ? -1f : 1f;
            Vector3 dir = new Vector3(sx * 0.35f, 0.2f, 0.9f).normalized;
            Transform oeil = Part(PrimitiveType.Sphere, i == 0 ? "Oeil_G" : "Oeil_D", corps,
                                  dir * R * 0.9f, Vector3.zero, new Vector3(0.38f * R, 0.45f * R, 0.2f * R), mYeux);
            oeil.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            Part(PrimitiveType.Sphere, "Pupille", oeil, new Vector3(0f, -0.08f, 0.35f), Vector3.zero,
                 new Vector3(0.5f, 0.55f, 0.6f), mNoir);
            yeux[i] = oeil;
        }

        // --- Accessoire ---
        if (accessoire == Accessoire.ChapeauPirate)
        {
            Material mBlanc = Mat("Boule_Blanc", Color.white, 0.5f, 0f);
            Part(PrimitiveType.Cylinder, "Chapeau_Bord", corps, new Vector3(0f, 0.9f * R, 0f), Vector3.zero,
                 new Vector3(1.4f * R, 0.03f * R, 1.1f * R), mNoir);
            Part(PrimitiveType.Sphere, "Chapeau_Haut", corps, new Vector3(0f, 0.95f * R, 0f), Vector3.zero,
                 new Vector3(0.9f * R, 0.55f * R, 0.8f * R), mNoir);
            Part(PrimitiveType.Sphere, "Chapeau_Crane", corps, new Vector3(0f, 1.05f * R, 0.38f * R), Vector3.zero,
                 Vector3.one * 0.14f * R, mBlanc);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(this);
        Debug.Log($"[NpcLook] {name} construit.");
    }

    // Cône à 6 faces, base de rayon 1 en y=0, pointe en y=1
    private static Mesh SpikeMesh()
    {
        EnsureFolder(MeshFolder);
        string path = $"{MeshFolder}/NPC_Pique.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();

        const int sides = 6;
        var v = new System.Collections.Generic.List<Vector3>();
        Vector3 apex = Vector3.up;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            Vector3 b0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            Vector3 b1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            v.Add(b0); v.Add(apex); v.Add(b1);
            v.Add(b0); v.Add(b1); v.Add(Vector3.zero);
        }
        var tris = new int[v.Count];
        for (int i = 0; i < tris.Length; i++) tris[i] = i;

        mesh.Clear();
        mesh.name = "NPC_Pique";
        mesh.SetVertices(v);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private Transform Empty(string n, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(n);
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    private Transform Part(PrimitiveType type, string n, Transform parent, Vector3 localPos, Vector3 euler,
                           Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = n;
        go.layer = gameObject.layer;
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    private static Material Mat(string n, Color c, float smooth, float emission)
    {
        EnsureFolder(MatFolder);
        string path = $"{MatFolder}/M_PNJ_{n}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 1f));
        m.SetFloat("_Smoothness", smooth);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * emission);
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
#endif
}