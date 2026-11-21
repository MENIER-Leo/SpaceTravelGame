using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Maison de la planète sombre. Clic droit > "Construire la maison", puis "Poser sur la planète".
public class MaisonLook : MonoBehaviour
{
    public enum Modele { Hutte, Tour, Double }

    [Header("Construction")]
    [SerializeField] private Modele modele = Modele.Hutte;
    [SerializeField] private float taille = 1f;
    [SerializeField] private int graine = 1;
    [SerializeField, Tooltip("Garde une collision sur le corps principal.")]
    private bool collision = true;
    [SerializeField] private bool lumiereEntree = true;
    [SerializeField] private bool cristalSurLeToit = true;

    [Header("Couleurs")]
    [SerializeField] private Color couleurMur = new Color(0.16f, 0.14f, 0.2f);
    [SerializeField] private Color couleurToit = new Color(0.28f, 0.12f, 0.38f);
    [SerializeField] private Color couleurPorte = new Color(0.08f, 0.06f, 0.1f);
    [SerializeField] private Color couleurFenetre = new Color(1f, 0.75f, 0.4f);
    [SerializeField] private float lueurFenetre = 2f;
    [SerializeField] private Color couleurCristal = new Color(0.6f, 0.4f, 1f);

    [Header("Placement")]
    [SerializeField, Tooltip("Centre de la planète (objet planète).")]
    private Transform planete;
    [SerializeField] private LayerMask sol = ~0;
    [SerializeField] private float enfoncement = 0.05f;

#if UNITY_EDITOR
    private const string Folder = "Assets/Deco/Maisons";
    private System.Random rng;
    private Material mMur, mToit, mPorte, mFenetre, mCadre, mCristal;
    private Mesh mCone, mBipyramide;
    private bool colliderPose;

    [ContextMenu("Construire la maison")]
    private void Build()
    {
        Transform old = transform.Find("Deco_Maison");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        EnsureFolder(Folder + "/Materials");
        EnsureFolder(Folder + "/Meshes");
        rng = new System.Random(graine);
        colliderPose = false;

        mMur = Mat("Mur", couleurMur, 0.1f, 0f);
        mToit = Mat("Toit", couleurToit, 0.2f, 0f);
        mPorte = Mat("Porte", couleurPorte, 0.3f, 0f);
        mCadre = Mat("Cadre", couleurMur * 0.6f, 0.2f, 0f);
        mFenetre = Mat("Fenetre", couleurFenetre, 0.8f, lueurFenetre);
        mCristal = Mat("Cristal", couleurCristal, 0.9f, 2f);
        mCone = ConeMesh();
        mBipyramide = BipyramidMesh();

        var rootGO = new GameObject("Deco_Maison");
        Undo.RegisterCreatedObjectUndo(rootGO, "Maison");
        rootGO.layer = gameObject.layer;
        Transform root = rootGO.transform;
        root.SetParent(transform, false);

        float T = taille;
        switch (modele)
        {
            case Modele.Hutte:
                Hutte(root, Vector3.zero, T, 0f);
                break;
            case Modele.Tour:
                Tour(root, T);
                break;
            case Modele.Double:
                Hutte(root, Vector3.zero, T, 0f);
                Hutte(root, new Vector3(1.3f * T, 0f, -0.4f * T), 0.65f * T, -60f);
                break;
        }

        if (lumiereEntree)
        {
            GameObject l = new GameObject("Lumiere_Entree");
            l.transform.SetParent(root, false);
            l.transform.localPosition = new Vector3(0f, 0.5f * T, (modele == Modele.Tour ? 1.1f : 1.4f) * T);
            Light light = l.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = couleurFenetre;
            light.intensity = 0.8f;
            light.range = 2.5f * T;
            light.shadows = LightShadows.None;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[MaisonLook] {name} : {modele} construite.");
    }

    private float Rand(float min, float max) => Mathf.Lerp(min, max, (float)rng.NextDouble());

    // Dôme à moitié enterré, porte arrondie, fenêtres rondes
    private void Hutte(Transform root, Vector3 pos, float T, float yaw)
    {
        Transform h = Group("Hutte", root, pos, yaw);
        float sx = Rand(0.95f, 1.1f);
        float rx = 1.1f * T * sx, ry = 1.2f * T;

        Part(PrimitiveType.Sphere, "Dome", h, Vector3.zero, Vector3.zero, new Vector3(rx * 2f, ry * 2f, rx * 2f), mMur, true);
        Part(PrimitiveType.Sphere, "Toit", h, new Vector3(0f, 0.75f * ry, 0f), Vector3.zero,
             new Vector3(rx * 1.5f, ry * 0.75f, rx * 1.5f), mToit, false);

        // Porte
        float zp = rx * 0.95f;
        Part(PrimitiveType.Cube, "Porte", h, new Vector3(0f, 0.3f * T, zp), Vector3.zero, new Vector3(0.5f * T, 0.6f * T, 0.2f * T), mPorte, false);
        Part(PrimitiveType.Cylinder, "Porte_Arc", h, new Vector3(0f, 0.6f * T, zp - 0.02f * T), new Vector3(90f, 0f, 0f),
             new Vector3(0.5f * T, 0.1f * T, 0.5f * T), mPorte, false);
        Part(PrimitiveType.Sphere, "Poignee", h, new Vector3(0.15f * T, 0.35f * T, zp + 0.11f * T), Vector3.zero,
             Vector3.one * 0.06f * T, mFenetre, false);

        // Fenêtres rondes de part et d'autre
        foreach (float a in new[] { -55f, 55f })
        {
            if (rng.NextDouble() < 0.2) continue;
            float y = 0.55f * ry;
            float rr = rx * Mathf.Sqrt(1f - (y / ry) * (y / ry));
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            Vector3 p = dir * rr + Vector3.up * y;
            PartRot(PrimitiveType.Cylinder, "Cadre", h, p, rot, new Vector3(0.42f * T, 0.05f * T, 0.42f * T), mCadre);
            PartRot(PrimitiveType.Cylinder, "Fenetre", h, p + dir * 0.03f * T, rot, new Vector3(0.32f * T, 0.05f * T, 0.32f * T), mFenetre);
        }

        // Cheminée
        if (rng.NextDouble() > 0.4)
            Part(PrimitiveType.Cylinder, "Cheminee", h, new Vector3(0.45f * rx, 0.95f * ry, -0.3f * rx), Vector3.zero,
                 new Vector3(0.22f * T, 0.25f * T, 0.22f * T), mMur, false);

        if (cristalSurLeToit)
            MeshPart("Cristal", h, new Vector3(0f, 1.1f * ry, 0f), Quaternion.Euler(0f, Rand(0f, 60f), 0f),
                     Vector3.one * 0.12f * T, mBipyramide, mCristal);
    }

    // Tour ronde avec toit pointu
    private void Tour(Transform root, float T)
    {
        Transform t = Group("Tour", root, Vector3.zero, 0f);
        float hgt = Rand(2.2f, 2.8f) * T;
        float r = 0.7f * T;

        Part(PrimitiveType.Cylinder, "Corps", t, new Vector3(0f, hgt / 2f, 0f), Vector3.zero, new Vector3(r * 2f, hgt / 2f, r * 2f), mMur, true);
        Part(PrimitiveType.Cylinder, "Corniche", t, new Vector3(0f, hgt, 0f), Vector3.zero, new Vector3(r * 2.3f, 0.06f * T, r * 2.3f), mCadre, false);
        MeshPart("Toit", t, new Vector3(0f, hgt, 0f), Quaternion.identity, new Vector3(r * 1.35f, 1.1f * T, r * 1.35f), mCone, mToit);

        // Porte
        Part(PrimitiveType.Cube, "Porte", t, new Vector3(0f, 0.3f * T, r), Vector3.zero, new Vector3(0.45f * T, 0.6f * T, 0.15f * T), mPorte, false);
        Part(PrimitiveType.Cylinder, "Porte_Arc", t, new Vector3(0f, 0.6f * T, r), new Vector3(90f, 0f, 0f),
             new Vector3(0.45f * T, 0.075f * T, 0.45f * T), mPorte, false);

        // Fenêtres
        foreach (float a in new[] { 0f, 120f, -120f })
        {
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            Vector3 p = dir * r + Vector3.up * hgt * 0.7f;
            PartRot(PrimitiveType.Cylinder, "Cadre", t, p, rot, new Vector3(0.36f * T, 0.04f * T, 0.36f * T), mCadre);
            PartRot(PrimitiveType.Cylinder, "Fenetre", t, p + dir * 0.03f * T, rot, new Vector3(0.27f * T, 0.04f * T, 0.27f * T), mFenetre);
        }

        if (cristalSurLeToit)
            MeshPart("Cristal", t, new Vector3(0f, hgt + 1.15f * T, 0f), Quaternion.identity, Vector3.one * 0.1f * T, mBipyramide, mCristal);
    }

    [ContextMenu("Poser sur la planète")]
    private void PoserSurPlanete()
    {
        if (planete == null) { Debug.LogError($"{name} : renseigne le champ Planete."); return; }
        Undo.RecordObject(transform, "Poser maison");

        Vector3 up = (transform.position - planete.position).normalized;
        transform.rotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;

        RaycastHit[] hits = Physics.RaycastAll(transform.position + up * 20f, -up, 60f, sol, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            transform.position = hit.point - up * enfoncement * taille;
            return;
        }
        Debug.LogWarning($"{name} : aucun sol trouvé sous la maison.");
    }

    private Transform Group(string n, Transform parent, Vector3 pos, float yaw)
    {
        var go = new GameObject(n);
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return go.transform;
    }

    private Transform Part(PrimitiveType type, string n, Transform parent, Vector3 localPos, Vector3 euler,
                           Vector3 scale, Material mat, bool garderCollider)
    {
        Transform t = PartRot(type, n, parent, localPos, Quaternion.Euler(euler), scale, mat, garderCollider);
        return t;
    }

    private Transform PartRot(PrimitiveType type, string n, Transform parent, Vector3 localPos, Quaternion rot,
                              Vector3 scale, Material mat, bool garderCollider = false)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = n;
        go.layer = gameObject.layer;
        bool keep = garderCollider && collision && !colliderPose;
        if (keep) colliderPose = true;
        else DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    private void MeshPart(string n, Transform parent, Vector3 localPos, Quaternion rot, Vector3 scale, Mesh mesh, Material mat)
    {
        var go = new GameObject(n);
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // Cône 8 faces : base rayon 1 en y=0, pointe en y=1
    private static Mesh ConeMesh()
    {
        return SaveMesh("Maison_Cone", v =>
        {
            const int sides = 8;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 b0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), b1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                v.Add(b0); v.Add(Vector3.up); v.Add(b1);
                v.Add(b0); v.Add(b1); v.Add(Vector3.zero);
            }
        });
    }

    // Double pyramide hexagonale : ceinture rayon 1, pointes y=1.8 et y=-1.2
    private static Mesh BipyramidMesh()
    {
        return SaveMesh("Maison_Bipyramide", v =>
        {
            const int sides = 6;
            Vector3 top = new Vector3(0f, 1.8f, 0f), bottom = new Vector3(0f, -1.2f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 g0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), g1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                v.Add(g0); v.Add(top); v.Add(g1);
                v.Add(g0); v.Add(g1); v.Add(bottom);
            }
        });
    }

    private static Mesh SaveMesh(string name, System.Action<List<Vector3>> build)
    {
        var v = new List<Vector3>();
        build(v);
        var tris = new int[v.Count];
        for (int i = 0; i < tris.Length; i++) tris[i] = i;

        string path = $"{Folder}/Meshes/{name}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        mesh.Clear();
        mesh.name = name;
        mesh.SetVertices(v);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Material Mat(string n, Color c, float smooth, float emission)
    {
        string path = $"{Folder}/Materials/M_Maison_{n}_{ColorUtility.ToHtmlStringRGB(c)}.mat";
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
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
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
