using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Potager de laitues. Tristes au départ, elles reprennent vie avec Revive().
/// Clic droit sur le composant > "Construire le potager".
public class PotagerLook : MonoBehaviour
{
    [Header("Construction")]
    [SerializeField] private int rangees = 2;
    [SerializeField] private int colonnes = 3;
    [SerializeField] private float espacement = 0.45f;
    [SerializeField] private float tailleLaitue = 0.35f;
    [SerializeField] private int graine = 3;
    [SerializeField] private bool arrosoir = true;
    [SerializeField] private bool panneau = true;
    [SerializeField] private Color couleurTerre = new Color(0.22f, 0.15f, 0.1f);
    [SerializeField] private Color couleurBois = new Color(0.35f, 0.24f, 0.15f);
    [SerializeField] private Color couleurMetal = new Color(0.35f, 0.45f, 0.55f);

    [Header("Laitues")]
    [SerializeField] private Color couleurSaine = new Color(0.45f, 0.85f, 0.35f);
    [SerializeField] private Color couleurTriste = new Color(0.5f, 0.5f, 0.22f);
    [SerializeField, Tooltip("Inclinaison supplémentaire des feuilles quand elles sont tristes.")]
    private float affaissement = 50f;
    [SerializeField] private float dureeReveil = 2.5f;
    [SerializeField] private bool saineAuDepart = false;

    [Header("Lumière du cristal")]
    [SerializeField] private Color couleurLumiere = new Color(0.4f, 0.9f, 0.85f);
    [SerializeField] private float intensiteLumiere = 1.5f;
    [SerializeField, Tooltip("M_Particule_Lumiere. Vide = pas d'étincelles.")]
    private Material materiauParticules;

    [SerializeField, HideInInspector] private Transform[] laitues;
    [SerializeField, HideInInspector] private Transform[] pivots;
    [SerializeField, HideInInspector] private float[] yaws;
    [SerializeField, HideInInspector] private float[] tilts;
    [SerializeField, HideInInspector] private Renderer[] feuilles;
    [SerializeField, HideInInspector] private Light lumiere;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock block;
    private float etat, cible;

    private void Awake()
    {
        etat = cible = saineAuDepart ? 1f : 0f;
        ApplyState(etat);
    }

    private void Update()
    {
        if (Mathf.Approximately(etat, cible)) return;
        etat = Mathf.MoveTowards(etat, cible, Time.deltaTime / Mathf.Max(0.01f, dureeReveil));
        ApplyState(Mathf.SmoothStep(0f, 1f, etat));
    }

    /// Les laitues reprennent vie.
    public void Revive()
    {
        if (cible >= 1f) return;
        cible = 1f;
        if (materiauParticules != null && laitues != null)
            foreach (Transform l in laitues)
                if (l != null) VfxUtil.Sparkles(l.position, transform.up, couleurSaine, materiauParticules, 12);
    }

    /// Les laitues redeviennent tristes.
    public void Wilt() => cible = 0f;

    private void ApplyState(float k)
    {
        if (pivots == null) return;
        if (block == null) block = new MaterialPropertyBlock();

        for (int i = 0; i < pivots.Length; i++)
            if (pivots[i] != null)
                pivots[i].localRotation = Quaternion.Euler(0f, yaws[i], 0f) *
                                          Quaternion.Euler(tilts[i] + affaissement * (1f - k), 0f, 0f);

        if (laitues != null)
            foreach (Transform l in laitues)
                if (l != null) l.localScale = new Vector3(1f, Mathf.Lerp(0.7f, 1f, k), 1f);

        block.SetColor(BaseColorId, Color.Lerp(couleurTriste, couleurSaine, k));
        if (feuilles != null)
            foreach (Renderer r in feuilles)
                if (r != null) r.SetPropertyBlock(block);

        if (lumiere != null)
        {
            lumiere.intensity = intensiteLumiere * k;
            lumiere.enabled = k > 0.01f;
        }
    }

#if UNITY_EDITOR
    private const string MatFolder = "Assets/Npc/Materials/Potager";

    [ContextMenu("Construire le potager")]
    private void Build()
    {
        Undo.RecordObject(this, "Potager");
        Transform old = transform.Find("Deco_Potager");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        EnsureFolder(MatFolder);
        Material mTerre = Mat("M_Potager_Terre", couleurTerre, 0.05f);
        Material mBois = Mat("M_Potager_Bois", couleurBois, 0.15f);
        Material mMetal = Mat("M_Potager_Metal", couleurMetal, 0.6f);
        Material mFeuille = Mat("M_Potager_Feuille", couleurSaine, 0.4f);

        var rootGO = new GameObject("Deco_Potager");
        Undo.RegisterCreatedObjectUndo(rootGO, "Potager");
        rootGO.layer = gameObject.layer;
        Transform root = rootGO.transform;
        root.SetParent(transform, false);

        float w = colonnes * espacement + 0.3f;
        float d = rangees * espacement + 0.3f;

        // Bac de terre et bordures
        Part(PrimitiveType.Cube, "Terre", root, new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(w, 0.1f, d), mTerre);
        Part(PrimitiveType.Cube, "Bord_Avant", root, new Vector3(0f, 0.08f, d / 2f), Vector3.zero, new Vector3(w + 0.06f, 0.16f, 0.06f), mBois);
        Part(PrimitiveType.Cube, "Bord_Arriere", root, new Vector3(0f, 0.08f, -d / 2f), Vector3.zero, new Vector3(w + 0.06f, 0.16f, 0.06f), mBois);
        Part(PrimitiveType.Cube, "Bord_G", root, new Vector3(-w / 2f, 0.08f, 0f), Vector3.zero, new Vector3(0.06f, 0.16f, d), mBois);
        Part(PrimitiveType.Cube, "Bord_D", root, new Vector3(w / 2f, 0.08f, 0f), Vector3.zero, new Vector3(0.06f, 0.16f, d), mBois);

        // Laitues
        var rng = new System.Random(graine);
        var lList = new List<Transform>();
        var pList = new List<Transform>();
        var yList = new List<float>();
        var tList = new List<float>();
        var fList = new List<Renderer>();

        for (int r = 0; r < rangees; r++)
            for (int c = 0; c < colonnes; c++)
            {
                float x = (c - (colonnes - 1) / 2f) * espacement;
                float z = (r - (rangees - 1) / 2f) * espacement;
                float s = tailleLaitue * (0.85f + 0.3f * (float)rng.NextDouble());

                GameObject lGO = new GameObject("Laitue");
                lGO.layer = gameObject.layer;
                Transform l = lGO.transform;
                l.SetParent(root, false);
                l.localPosition = new Vector3(x, 0.1f, z);
                l.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                lList.Add(l);

                fList.Add(Part(PrimitiveType.Sphere, "Coeur", l, new Vector3(0f, 0.15f * s, 0f), Vector3.zero,
                               Vector3.one * 0.35f * s, mFeuille).GetComponent<Renderer>());

                // Couronne extérieure (6) puis intérieure (4)
                for (int ring = 0; ring < 2; ring++)
                {
                    int n = ring == 0 ? 6 : 4;
                    float tilt = ring == 0 ? 30f : 12f;
                    float off = ring == 0 ? 0f : 45f;
                    Vector3 leafPos = new Vector3(0f, (ring == 0 ? 0.3f : 0.22f) * s, 0f);
                    Vector3 leafScale = ring == 0 ? new Vector3(0.45f, 0.6f, 0.15f) * s : new Vector3(0.35f, 0.45f, 0.12f) * s;

                    for (int i = 0; i < n; i++)
                    {
                        float yaw = off + i * 360f / n + ((float)rng.NextDouble() - 0.5f) * 15f;
                        Transform pivot = new GameObject("Pivot").transform;
                        pivot.gameObject.layer = gameObject.layer;
                        pivot.SetParent(l, false);
                        pivot.localPosition = new Vector3(0f, 0.05f * s, 0f);
                        fList.Add(Part(PrimitiveType.Sphere, "Feuille", pivot, leafPos, Vector3.zero, leafScale, mFeuille)
                                  .GetComponent<Renderer>());
                        pList.Add(pivot);
                        yList.Add(yaw);
                        tList.Add(tilt);
                    }
                }
            }

        laitues = lList.ToArray();
        pivots = pList.ToArray();
        yaws = yList.ToArray();
        tilts = tList.ToArray();
        feuilles = fList.ToArray();

        // Arrosoir
        if (arrosoir)
        {
            Transform a = new GameObject("Arrosoir").transform;
            a.gameObject.layer = gameObject.layer;
            a.SetParent(root, false);
            a.localPosition = new Vector3(w / 2f + 0.25f, 0f, -d / 4f);
            a.localRotation = Quaternion.Euler(0f, 30f, 0f);
            Part(PrimitiveType.Cylinder, "Corps", a, new Vector3(0f, 0.11f, 0f), Vector3.zero, new Vector3(0.18f, 0.11f, 0.18f), mMetal);
            Part(PrimitiveType.Cylinder, "Bec", a, new Vector3(0.13f, 0.15f, 0f), new Vector3(0f, 0f, -50f), new Vector3(0.03f, 0.12f, 0.03f), mMetal);
            Part(PrimitiveType.Cube, "Anse", a, new Vector3(0f, 0.26f, 0f), Vector3.zero, new Vector3(0.14f, 0.025f, 0.025f), mMetal);
            Part(PrimitiveType.Cube, "Anse_G", a, new Vector3(-0.06f, 0.24f, 0f), Vector3.zero, new Vector3(0.025f, 0.05f, 0.025f), mMetal);
            Part(PrimitiveType.Cube, "Anse_D", a, new Vector3(0.06f, 0.24f, 0f), Vector3.zero, new Vector3(0.025f, 0.05f, 0.025f), mMetal);
        }

        // Panneau
        if (panneau)
        {
            Vector3 p = new Vector3(-w / 2f - 0.2f, 0f, d / 2f);
            Part(PrimitiveType.Cube, "Panneau_Poteau", root, p + new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(0.05f, 0.5f, 0.05f), mBois);
            Part(PrimitiveType.Cube, "Panneau", root, p + new Vector3(0f, 0.45f, 0.03f), new Vector3(0f, 0f, -5f), new Vector3(0.35f, 0.18f, 0.03f), mBois);
        }

        // Lumière bleu-vert (éteinte tant que les laitues sont tristes)
        GameObject lg = new GameObject("Lumiere_Potager");
        lg.transform.SetParent(root, false);
        lg.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        lumiere = lg.AddComponent<Light>();
        lumiere.type = LightType.Point;
        lumiere.color = couleurLumiere;
        lumiere.range = Mathf.Max(w, d) * 1.8f;
        lumiere.shadows = LightShadows.None;

        ApplyState(saineAuDepart ? 1f : 0f);
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(this);
        Debug.Log($"[PotagerLook] {laitues.Length} laitues construites.");
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

    private static Material Mat(string n, Color c, float smooth)
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
