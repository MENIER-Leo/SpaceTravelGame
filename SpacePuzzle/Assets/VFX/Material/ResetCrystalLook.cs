using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Look du cristal de reset : cristal double pointe flottant, éclats en orbite, socle, lumière.
/// Clic droit sur le composant > "Construire le look". Appeler Pulse() au moment du reset.
public class ResetCrystalLook : MonoBehaviour
{
    [Header("Construction")]
    [SerializeField, Tooltip("Prend hauteur et base depuis la BoxCollider de la racine si elle existe.")]
    private bool depuisCollider = true;
    [SerializeField] private float hauteur = 1.5f;
    [SerializeField] private float baseY = -0.75f;
    [SerializeField] private int nombreEclats = 3;
    [SerializeField] private Color couleur = new Color(0.75f, 0.95f, 1f);
    [SerializeField] private Color couleurSocle = new Color(0.18f, 0.17f, 0.22f);
    [SerializeField] private bool ajouterLumiere = true;
    [SerializeField, Tooltip("Renderers d'origine masqués. Vide = rempli automatiquement.")]
    private List<Renderer> aMasquer = new List<Renderer>();

    [Header("Animation")]
    [SerializeField] private float rotationSpeed = 25f;
    [SerializeField] private float orbitSpeed = 40f;
    [SerializeField] private float bobAmount = 0.05f;
    [SerializeField] private float bobSpeed = 0.3f;
    [SerializeField] private float glow = 1.5f;
    [SerializeField] private float glowPulse = 0.4f;
    [SerializeField] private float glowPulseSpeed = 0.5f;
    [SerializeField] private float lightIntensity = 1.2f;

    [Header("Impulsion (Pulse)")]
    [SerializeField] private float pulseDuration = 1f;
    [SerializeField] private float pulseGlow = 4f;
    [SerializeField] private float pulseOrbitBoost = 6f;
    [SerializeField, Tooltip("M_Particule_Lumiere. Vide = pas de particules.")]
    private Material materiauParticules;

    [SerializeField, HideInInspector] private Transform coeur;
    [SerializeField, HideInInspector] private Transform orbite;
    [SerializeField, HideInInspector] private Renderer[] lumineux;
    [SerializeField, HideInInspector] private Light lumiere;
    [SerializeField, HideInInspector] private float tailleRef = 1.5f;

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock block;
    private Vector3 coeurPos;
    private float pulse;

    private void Start()
    {
        block = new MaterialPropertyBlock();
        if (coeur != null) coeurPos = coeur.localPosition;
    }

    private void Update()
    {
        if (coeur == null) return;
        float t = Time.time;
        float dt = Time.deltaTime;
        pulse = Mathf.MoveTowards(pulse, 0f, dt / Mathf.Max(0.01f, pulseDuration));

        coeur.localRotation = Quaternion.Euler(0f, t * rotationSpeed, 0f);
        coeur.localPosition = coeurPos + Vector3.up * Mathf.Sin(t * bobSpeed * Mathf.PI * 2f) * bobAmount * tailleRef;

        if (orbite != null)
            orbite.localRotation *= Quaternion.Euler(0f, orbitSpeed * (1f + pulse * pulseOrbitBoost) * dt, 0f);

        float g = glow + Mathf.Sin(t * glowPulseSpeed * Mathf.PI * 2f) * glowPulse + pulse * pulseGlow;
        block.SetColor(EmissionId, couleur * g);
        if (lumineux != null)
            foreach (Renderer r in lumineux)
                if (r != null) r.SetPropertyBlock(block);

        if (lumiere != null) lumiere.intensity = lightIntensity * g / Mathf.Max(0.01f, glow);
    }

    /// À appeler au moment du reset.
    public void Pulse()
    {
        pulse = 1f;
        if (materiauParticules != null && coeur != null)
        {
            VfxUtil.Ring(coeur.position, transform.up, couleur, materiauParticules);
            VfxUtil.Glow(coeur.position, couleur, materiauParticules, tailleRef * 1.5f);
        }
    }

#if UNITY_EDITOR
    private const string MatFolder = "Assets/Crystals/Materials";
    private const string MeshFolder = "Assets/Crystals/Meshes";

    [ContextMenu("Construire le look")]
    private void Build()
    {
        Undo.RecordObject(this, "Look reset");

        if (depuisCollider)
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null) { hauteur = box.size.y; baseY = box.center.y - box.size.y * 0.5f; }
        }
        float H = hauteur;
        tailleRef = H;

        Transform old = transform.Find("Look_Reset");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // Masque les visuels d'origine (pas les textes 3D)
        if (aMasquer.Count == 0)
            foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
                if (r.GetComponent<TMPro.TMP_Text>() == null) aMasquer.Add(r);
        foreach (Renderer r in aMasquer)
            if (r != null) { Undo.RecordObject(r, "Look reset"); r.enabled = false; }

        EnsureFolder(MatFolder);
        EnsureFolder(MeshFolder);
        Material mCristal = Mat("M_Reset_Cristal", couleur, 0.9f, true);
        Material mSocle = Mat("M_Reset_Socle", couleurSocle, 0.15f, false);
        Mesh bipyramide = BipyramidMesh();
        var glowing = new List<Renderer>();

        var rootGO = new GameObject("Look_Reset");
        Undo.RegisterCreatedObjectUndo(rootGO, "Look reset");
        rootGO.layer = gameObject.layer;
        Transform root = rootGO.transform;
        root.SetParent(transform, false);
        root.localPosition = new Vector3(0f, baseY, 0f);

        // Socle + petits rochers
        Part(PrimitiveType.Sphere, "Socle", root, new Vector3(0f, 0.02f * H, 0f), new Vector3(0f, 20f, 0f),
             new Vector3(0.55f * H, 0.15f * H, 0.55f * H), mSocle);
        for (int i = 0; i < 4; i++)
        {
            float a = (i * 90f + 40f) * Mathf.Deg2Rad;
            Part(PrimitiveType.Sphere, "Rocher", root,
                 new Vector3(Mathf.Cos(a) * 0.3f * H, 0.02f * H, Mathf.Sin(a) * 0.3f * H),
                 new Vector3(15f * i, 37f * i, 0f), Vector3.one * (0.1f + 0.03f * (i % 2)) * H, mSocle);
        }
        glowing.Add(Part(PrimitiveType.Cylinder, "Plaque", root, new Vector3(0f, 0.08f * H, 0f), Vector3.zero,
                         new Vector3(0.38f * H, 0.005f * H, 0.38f * H), mCristal).GetComponent<Renderer>());

        // Coeur (tourne et flotte)
        coeur = Empty("Coeur", root, new Vector3(0f, 0.55f * H, 0f));
        glowing.Add(MeshPart("Cristal", coeur, Vector3.zero, Quaternion.identity, Vector3.one * 0.16f * H,
                             bipyramide, mCristal));

        // Éclats en orbite
        orbite = Empty("Orbite", root, new Vector3(0f, 0.5f * H, 0f));
        for (int i = 0; i < nombreEclats; i++)
        {
            float a = i * 360f / nombreEclats * Mathf.Deg2Rad;
            Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.32f * H;
            glowing.Add(MeshPart("Eclat", orbite, p, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 20f),
                                 Vector3.one * 0.055f * H, bipyramide, mCristal));
        }
        lumineux = glowing.ToArray();

        // Lumière
        lumiere = null;
        if (ajouterLumiere)
        {
            GameObject l = new GameObject("Lumiere");
            l.transform.SetParent(coeur, false);
            lumiere = l.AddComponent<Light>();
            lumiere.type = LightType.Point;
            lumiere.color = couleur;
            lumiere.range = 2.5f * H;
            lumiere.intensity = lightIntensity;
            lumiere.shadows = LightShadows.None;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(this);
        Debug.Log($"[ResetCrystalLook] {name} construit, {aMasquer.Count} renderer(s) masqué(s).");
    }

    // Double pyramide hexagonale : ceinture rayon 1 en y=0, pointe haute y=1.8, basse y=-1.2
    private static Mesh BipyramidMesh()
    {
        string path = $"{MeshFolder}/Reset_Bipyramide.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();

        const int sides = 6;
        var v = new List<Vector3>();
        Vector3 top = new Vector3(0f, 1.8f, 0f), bottom = new Vector3(0f, -1.2f, 0f);
        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            Vector3 g0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            Vector3 g1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            v.Add(g0); v.Add(top); v.Add(g1);
            v.Add(g0); v.Add(g1); v.Add(bottom);
        }
        var tris = new int[v.Count];
        for (int i = 0; i < tris.Length; i++) tris[i] = i;

        mesh.Clear();
        mesh.name = "Reset_Bipyramide";
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

    private Renderer MeshPart(string n, Transform parent, Vector3 localPos, Quaternion rot, Vector3 scale,
                              Mesh mesh, Material mat)
    {
        var go = new GameObject(n);
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        return r;
    }

    private static Material Mat(string n, Color c, float smooth, bool emission)
    {
        string path = $"{MatFolder}/{n}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 1f));
        m.SetFloat("_Smoothness", smooth);
        if (emission)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c);
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
