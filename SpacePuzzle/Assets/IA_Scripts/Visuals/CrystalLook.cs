using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Look des cristaux : maillage facetté par type, teinte, socle rocheux, léger flottement.
/// À placer sur le prefab Crystal. En mode prefab : clic droit > "Générer le look".
[RequireComponent(typeof(Crystal))]
public class CrystalLook : MonoBehaviour
{
    [Header("Références (auto si vides)")]
    [SerializeField] private Transform meshTransform;      // enfant "Mesh"
    [SerializeField] private Transform highlightTransform; // enfant "Highlight"

    [Header("Maillages (remplis par Générer le look)")]
    [SerializeField] private Mesh meshEmitter;
    [SerializeField] private Mesh meshRelay;
    [SerializeField] private Mesh meshReceiver;
    [SerializeField] private Mesh meshMixer;
    [SerializeField] private Mesh meshPrism;

    [Header("Placement")]
    [SerializeField, Tooltip("Hauteur de la base du cristal par rapport au pivot.")]
    private float baseOffset = -0.75f;
    [SerializeField] private float highlightScale = 1.12f;

    [Header("Couleurs")]
    [SerializeField] private Color relayColor = new Color(0.75f, 0.88f, 1f);
    [SerializeField] private Color mixerColor = new Color(0.85f, 0.75f, 1f);
    [SerializeField] private Color prismColor = new Color(0.95f, 0.95f, 1f);
    [SerializeField, Tooltip("Teinte les récepteurs avec leur couleur cible (indice visuel).")]
    private bool tintReceivers = true;
    [SerializeField, Range(0f, 1f), Tooltip("Lueur de repos quand le cristal est éteint.")]
    private float idleGlow = 0.15f;

    [Header("Flottement")]
    [SerializeField] private float bobAmplitude = 0.04f;
    [SerializeField] private float bobSpeed = 0.35f;

    private Crystal crystal;
    private Vector3 meshBase, highlightBase;
    private float phase;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        phase = Random.value * 10f;
        Apply();
    }

    private void Update()
    {
        if (bobAmplitude <= 0f || meshTransform == null) return;
        float y = Mathf.Sin((Time.time + phase) * bobSpeed * Mathf.PI * 2f) * bobAmplitude;
        meshTransform.localPosition = meshBase + Vector3.up * y;
        if (highlightTransform != null) highlightTransform.localPosition = highlightBase + Vector3.up * y;
    }

    private void Apply()
    {
        if (crystal == null) crystal = GetComponent<Crystal>();
        if (meshTransform == null) meshTransform = transform.Find("Mesh");
        if (highlightTransform == null) highlightTransform = transform.Find("Highlight");
        if (meshTransform == null) { Debug.LogWarning($"{name} : enfant Mesh introuvable."); return; }

        Mesh m = MeshFor(crystal.crystalType);
        MeshFilter mf = meshTransform.GetComponent<MeshFilter>();
        if (m != null && mf != null) mf.sharedMesh = m;
        meshTransform.localPosition = new Vector3(0f, baseOffset, 0f);
        meshTransform.localRotation = Quaternion.identity;
        meshTransform.localScale = Vector3.one;
        meshBase = meshTransform.localPosition;

        if (highlightTransform != null)
        {
            MeshFilter hf = highlightTransform.GetComponent<MeshFilter>();
            if (hf != null && m != null)
            {
                hf.sharedMesh = m;
                highlightTransform.localPosition = new Vector3(0f, baseOffset - 0.04f, 0f);
                highlightTransform.localRotation = Quaternion.identity;
                highlightTransform.localScale = Vector3.one * highlightScale;
            }
            highlightBase = highlightTransform.localPosition;
        }

        Color c = ColorFor();
        Renderer r = meshTransform.GetComponent<Renderer>();
        if (r != null)
        {
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(block);
        }
        crystal.idleEmission = new Color(c.r * idleGlow, c.g * idleGlow, c.b * idleGlow, 1f);
    }

    private Mesh MeshFor(CrystalType type)
    {
        switch (type)
        {
            case CrystalType.Emitter: return meshEmitter;
            case CrystalType.Receiver: return meshReceiver;
            case CrystalType.Mixer: return meshMixer;
            case CrystalType.Prism: return meshPrism;
            default: return meshRelay;
        }
    }

    private Color ColorFor()
    {
        switch (crystal.crystalType)
        {
            case CrystalType.Emitter:
                return Opaque(Color.Lerp(ColorsUtil.ToColor(crystal.emittedColor), Color.white, 0.2f));
            case CrystalType.Receiver:
                if (tintReceivers && crystal.targetColor != Colors.None)
                    return Opaque(Color.Lerp(ColorsUtil.ToColor(crystal.targetColor), Color.white, 0.35f) * 0.8f);
                return Opaque(relayColor * 0.7f);
            case CrystalType.Mixer: return mixerColor;
            case CrystalType.Prism: return prismColor;
            default: return relayColor;
        }
    }

    private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

#if UNITY_EDITOR
    private const string MeshFolder = "Assets/Crystals/Meshes";
    private const string MatFolder = "Assets/Crystals/Materials";

    [ContextMenu("Générer le look")]
    private void Generate()
    {
        Undo.RecordObject(this, "Look cristal");
        EnsureFolder(MeshFolder);
        EnsureFolder(MatFolder);

        meshEmitter = SaveMesh("Crystal_Emitter", v =>
        {
            Shard(v, 6, 0.26f, 1.0f, 0.45f, Vector3.zero, Quaternion.identity);
            Ring(v, 3, 0.26f, 6, 0.11f, 0.38f, 0.18f, 28f, 20f);
        });
        meshRelay = SaveMesh("Crystal_Relay", v =>
        {
            Shard(v, 6, 0.2f, 0.95f, 0.4f, Vector3.zero, Quaternion.identity);
            Ring(v, 2, 0.22f, 6, 0.09f, 0.3f, 0.14f, 30f, 40f);
        });
        meshReceiver = SaveMesh("Crystal_Receiver", v =>
        {
            Shard(v, 6, 0.3f, 0.55f, 0.3f, Vector3.zero, Quaternion.identity);
            Ring(v, 5, 0.32f, 6, 0.09f, 0.42f, 0.15f, 18f, 0f);
        });
        meshMixer = SaveMesh("Crystal_Mixer", v =>
        {
            Shard(v, 6, 0.17f, 0.85f, 0.35f, new Vector3(0.13f, 0f, 0f), Tilt(Vector3.right, 10f));
            Shard(v, 6, 0.17f, 0.85f, 0.35f, new Vector3(-0.13f, 0f, 0f), Tilt(Vector3.left, 10f));
        });
        meshPrism = SaveMesh("Crystal_Prism", v =>
        {
            Shard(v, 3, 0.32f, 0.85f, 0.35f, Vector3.zero, Quaternion.identity);
            Ring(v, 2, 0.3f, 3, 0.1f, 0.3f, 0.14f, 30f, 90f);
        });

        // Matériau facetté (émission activée, pilotée par Crystal)
        if (meshTransform == null) meshTransform = transform.Find("Mesh");
        Material facette = GetMat($"{MatFolder}/M_Crystal_Facette.mat", Color.white, 0.85f, true);
        if (meshTransform != null) meshTransform.GetComponent<Renderer>().sharedMaterial = facette;

        // Socle rocheux
        Transform old = transform.Find("Socle");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        GameObject socle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Undo.RegisterCreatedObjectUndo(socle, "Look cristal");
        socle.name = "Socle";
        DestroyImmediate(socle.GetComponent<Collider>());
        socle.transform.SetParent(transform, false);
        socle.transform.localPosition = new Vector3(0f, baseOffset + 0.02f, 0f);
        socle.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
        socle.transform.localScale = new Vector3(0.75f, 0.22f, 0.75f);
        socle.layer = gameObject.layer;
        socle.GetComponent<Renderer>().sharedMaterial =
            GetMat($"{MatFolder}/M_Crystal_Socle.mat", new Color(0.2f, 0.2f, 0.24f), 0.15f, false);

        Apply();
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(this);
        Debug.Log("[CrystalLook] Look généré.");
    }

    private static Mesh SaveMesh(string name, System.Action<List<Vector3>> build)
    {
        var verts = new List<Vector3>();
        build(verts);
        var tris = new int[verts.Count];
        for (int i = 0; i < tris.Length; i++) tris[i] = i;

        string path = $"{MeshFolder}/{name}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        mesh.Clear();
        mesh.name = name;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();   // sommets non partagés = facettes nettes
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // Pointe de cristal : prisme légèrement resserré + pointe
    private static void Shard(List<Vector3> v, int sides, float r, float h, float tip, Vector3 pos, Quaternion rot)
    {
        float rt = r * 0.88f;
        Vector3 apex = new Vector3(0f, h + tip, 0f);
        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            Vector3 b0 = new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
            Vector3 b1 = new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
            Vector3 t0 = new Vector3(Mathf.Cos(a0) * rt, h, Mathf.Sin(a0) * rt);
            Vector3 t1 = new Vector3(Mathf.Cos(a1) * rt, h, Mathf.Sin(a1) * rt);
            Tri(v, b0, t0, t1, pos, rot);
            Tri(v, b0, t1, b1, pos, rot);
            Tri(v, t0, apex, t1, pos, rot);
            Tri(v, b0, b1, Vector3.zero, pos, rot);
        }
    }

    // Petits éclats répartis en cercle et inclinés vers l'extérieur
    private static void Ring(List<Vector3> v, int count, float radius, int sides, float r, float h, float tip,
                             float tilt, float angle0)
    {
        for (int k = 0; k < count; k++)
        {
            float a = (angle0 + k * 360f / count) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Shard(v, sides, r, h, tip, dir * radius + Vector3.down * 0.05f, Tilt(dir, tilt));
        }
    }

    private static Quaternion Tilt(Vector3 dir, float degrees) =>
        Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, dir, degrees / 90f));

    private static void Tri(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c, Vector3 pos, Quaternion rot)
    {
        v.Add(pos + rot * a);
        v.Add(pos + rot * b);
        v.Add(pos + rot * c);
    }

    private static Material GetMat(string path, Color c, float smooth, bool emission)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        if (emission)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
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
