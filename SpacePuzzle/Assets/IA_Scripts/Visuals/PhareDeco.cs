using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// Décoration simple du Phare (bandes, galerie, toit, porte).
/// À placer sur Region_Phare. Clic droit sur le composant > "Construire la déco du Phare".
public class PhareDeco : MonoBehaviour
{
    [Header("Références (auto si vides)")]
    [SerializeField, Tooltip("Cylindre de la tour. Vide = recherche auto (cylindre sur le layer Ground).")]
    private Transform tour;
    [SerializeField, Tooltip("Sphère de la lanterne. Vide = parent de Lumiere_Phare.")]
    private Transform lanterne;

    [Header("Éléments")]
    [SerializeField] private bool bandes = true;
    [SerializeField, Range(1, 6)] private int nombreBandes = 3;
    [SerializeField] private bool galerie = true;
    [SerializeField, Range(6, 24)] private int poteaux = 12;
    [SerializeField] private bool toit = true;
    [SerializeField] private bool porte = true;
    [SerializeField, Tooltip("Angle de la porte autour de la tour, en degrés.")]
    private float anglePorte = 0f;

    [Header("Couleurs")]
    [SerializeField] private Color couleurBandes = new Color(0.80f, 0.12f, 0.10f);
    [SerializeField] private Color couleurMetal = new Color(0.18f, 0.20f, 0.22f);
    [SerializeField] private Color couleurToit = new Color(0.80f, 0.12f, 0.10f);
    [SerializeField] private Color couleurPorte = new Color(0.35f, 0.22f, 0.12f);

#if UNITY_EDITOR
    private const string MatFolder = "Assets/VFX/Materials/Phare";

    [ContextMenu("Construire la déco du Phare")]
    private void Build()
    {
        FindReferences();
        if (tour == null)
        {
            Debug.LogError("[PhareDeco] Tour introuvable : glisse le cylindre du Phare dans le champ Tour.");
            return;
        }

        Undo.RecordObject(this, "Déco Phare");
        Transform old = transform.Find("Deco_Phare");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var root = new GameObject("Deco_Phare").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Déco Phare");
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        // Géométrie de la tour (cylindre Unity : 2 unités de haut, 1 de diamètre)
        Vector3 u = tour.up;
        float H = tour.lossyScale.y * 2f;
        float R = tour.lossyScale.x * 0.5f;
        Vector3 B = tour.position - u * H * 0.5f;
        Vector3 T = tour.position + u * H * 0.5f;
        Vector3 fwd = Vector3.ProjectOnPlane(tour.forward, u).normalized;
        Quaternion rot = tour.rotation;

        // --- Bandes ---
        if (bandes)
        {
            Material m = Mat("M_Phare_Bandes", couleurBandes, 0.3f);
            int segments = nombreBandes * 2 + 1;
            float h = H / segments;
            for (int k = 0; k < nombreBandes; k++)
            {
                Vector3 c = B + u * h * (2 * k + 1.5f);
                Piece(PrimitiveType.Cylinder, $"Bande_{k + 1}", c, rot,
                      new Vector3(2f * R * 1.03f, h * 0.5f, 2f * R * 1.03f), m, root);
            }
        }

        // --- Galerie ---
        if (galerie)
        {
            Material m = Mat("M_Phare_Metal", couleurMetal, 0.5f);
            Piece(PrimitiveType.Cylinder, "Galerie_Plateforme", T + u * 0.075f * R, rot,
                  new Vector3(2f * R * 1.6f, 0.075f * R, 2f * R * 1.6f), m, root);

            float rr = R * 1.45f;
            Vector3 baseRail = T + u * 0.15f * R;
            float step = 360f / poteaux;
            for (int i = 0; i < poteaux; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i * step, u) * fwd;
                Piece(PrimitiveType.Cylinder, $"Poteau_{i + 1}", baseRail + u * 0.35f * R + dir * rr, rot,
                      new Vector3(0.07f * R, 0.35f * R, 0.07f * R), m, root);

                Vector3 mid = Quaternion.AngleAxis((i + 0.5f) * step, u) * fwd;
                float len = 2f * rr * Mathf.Sin(Mathf.PI / poteaux);
                Vector3 pos = baseRail + u * 0.7f * R + mid * rr * Mathf.Cos(Mathf.PI / poteaux);
                Piece(PrimitiveType.Cube, $"Rambarde_{i + 1}", pos, Quaternion.LookRotation(mid, u),
                      new Vector3(len, 0.06f * R, 0.06f * R), m, root);
            }
        }

        // --- Toit ---
        if (toit)
        {
            if (lanterne == null)
                Debug.LogWarning("[PhareDeco] Lanterne introuvable : toit ignoré.");
            else
            {
                Material m = Mat("M_Phare_Toit", couleurToit, 0.4f);
                float s = lanterne.lossyScale.x;
                Vector3 L = lanterne.position;
                Piece(PrimitiveType.Sphere, "Toit_Dome", L + u * 0.5f * s, rot,
                      new Vector3(1.25f * s, 0.4f * s, 1.25f * s), m, root);
                Piece(PrimitiveType.Sphere, "Toit_Pointe", L + u * 0.78f * s, rot,
                      Vector3.one * 0.18f * s, m, root);
            }
        }

        // --- Porte ---
        if (porte)
        {
            Material m = Mat("M_Phare_Porte", couleurPorte, 0.2f);
            Vector3 dir = Quaternion.AngleAxis(anglePorte, u) * fwd;
            Piece(PrimitiveType.Cube, "Porte", B + u * 0.9f + dir * (R - 0.05f), Quaternion.LookRotation(dir, u),
                  new Vector3(1f, 1.8f, 0.25f), m, root);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("[PhareDeco] Déco construite.");
    }

    private void FindReferences()
    {
        if (lanterne == null)
        {
            foreach (Light l in GetComponentsInChildren<Light>(true))
                if (l.name == "Lumiere_Phare") { lanterne = l.transform.parent; break; }
        }
        if (tour == null)
        {
            int ground = LayerMask.NameToLayer("Ground");
            foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.gameObject.layer == ground && mf.sharedMesh != null && mf.sharedMesh.name == "Cylinder"
                    && !mf.transform.IsChildOf(transform.Find("Deco_Phare") ?? mf.transform.parent))
                { tour = mf.transform; break; }
            }
        }
    }

    private static GameObject Piece(PrimitiveType type, string name, Vector3 pos, Quaternion rot,
                                    Vector3 scale, Material mat, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // ne gêne ni le joueur ni les rayons
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static Material Mat(string name, Color color, float smoothness)
    {
        EnsureFolder(MatFolder);
        string path = $"{MatFolder}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
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
