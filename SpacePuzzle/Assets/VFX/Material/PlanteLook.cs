using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Plante de la région verte : rosette, tige qui ondule, bulbe lumineux.
/// Clic droit sur le composant > "Construire la plante", puis en faire un prefab.
/// Se colle au sol (layer Planete) en jeu, et à la construction du puzzle.
/// Le bulbe s'intensifie quand le cristal parent reçoit un rayon.
public class PlanteLook : MonoBehaviour
{
    [Header("Forme")]
    [SerializeField] private float hauteur = 1.5f;
    [SerializeField] private int segments = 5;
    [SerializeField, Tooltip("Courbure totale de la tige, en degrés.")]
    private float courbure = 35f;
    [SerializeField] private float epaisseur = 0.07f;
    [SerializeField] private int feuillesSol = 4;
    [SerializeField] private int petales = 5;
    [SerializeField] private float tailleBulbe = 0.22f;
    [SerializeField] private bool lumiere = true;

    [Header("Couleurs")]
    [SerializeField] private Color couleurTige = new Color(0.12f, 0.35f, 0.18f);
    [SerializeField] private Color couleurFeuille = new Color(0.18f, 0.55f, 0.25f);
    [SerializeField] private Color couleurPetale = new Color(0.1f, 0.3f, 0.2f);
    [SerializeField] private Color couleurBulbe = new Color(0.4f, 1f, 0.5f);
    [SerializeField] private float lueurBulbe = 2.5f;

    [Header("Animation")]
    [SerializeField] private float swayAmount = 4f;
    [SerializeField] private float swaySpeed = 0.4f;
    [SerializeField] private float pulseAmount = 0.35f;
    [SerializeField] private float pulseSpeed = 0.6f;

    [Header("Réaction au rayon")]
    [SerializeField, Tooltip("Multiplicateur de lueur quand la plante est éclairée.")]
    private float lueurEclairee = 3f;
    [SerializeField, Tooltip("Vitesse de la transition éteinte ↔ éclairée (par seconde).")]
    private float vitesseTransition = 2.5f;
    [SerializeField, Tooltip("Multiplicateur de la vitesse de pulsation quand la plante est éclairée.")]
    private float pulseEclaire = 2f;

    [Header("Sol")]
    [SerializeField, Tooltip("Recolle la plante au sol au lancement du jeu.")]
    private bool collerAuSol = true;
    [SerializeField] private string layerSol = "Planete";
    [SerializeField, Tooltip("Décalage le long de la verticale après collage (négatif = enfoncée).")]
    private float decalageSol = -0.05f;

    [SerializeField, HideInInspector] private Transform[] pivots;
    [SerializeField, HideInInspector] private Quaternion[] basesRot;
    [SerializeField, HideInInspector] private Renderer bulbe;
    [SerializeField, HideInInspector] private Light lampe;

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock block;
    private Crystal cristal;
    private float phase;
    private float pulseTime;
    private float lightBase;
    private float eclairage;   // 0 = éteinte, 1 = éclairée

    private IEnumerator Start()
    {
        phase = Random.value * 10f;
        block = new MaterialPropertyBlock();
        cristal = GetComponentInParent<Crystal>();
        if (lampe != null) lightBase = lampe.intensity;

        if (collerAuSol)
        {
            yield return null;   // laisse le cristal se replacer d'abord
            CollerAuSol();
        }
    }

    /// Pose la base de la plante sur le sol, sous son parent (le cristal) ou sous elle-même.
    public bool CollerAuSol()
    {
        int mask = LayerMask.GetMask(layerSol);
        if (mask == 0)
        {
            Debug.LogWarning($"{name} : layer « {layerSol} » introuvable, plante non collée au sol.");
            return false;
        }

        Transform anchor = transform.parent != null ? transform.parent : transform;
        Vector3 up = anchor.up;
        BeamManager bm = GetComponentInParent<BeamManager>();
        if (bm != null && bm.PlanetTransform != null)
            up = (anchor.position - bm.PlanetTransform.position).normalized;

        const float above = 30f;
        Vector3 from = anchor.position + up * above;
        RaycastHit[] hits = Physics.RaycastAll(from, -up, above + 50f, mask, QueryTriggerInteraction.Ignore);

        bool found = false;
        float best = float.MaxValue;
        Vector3 point = Vector3.zero;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(anchor)) continue;   // ignore le cristal lui-même
            float d = Mathf.Abs(h.distance - above);                // surface la plus proche du cristal
            if (d < best) { best = d; point = h.point; found = true; }
        }

        if (!found)
        {
            Debug.LogWarning($"{name} : aucun sol trouvé sous la plante.");
            return false;
        }

        transform.rotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
        transform.position = point + up * decalageSol;
        return true;
    }

    private void Update()
    {
        float t = Time.time + phase;

        // Ondulation de la tige
        if (pivots != null)
            for (int i = 0; i < pivots.Length; i++)
            {
                if (pivots[i] == null) continue;
                float a = Mathf.Sin(t * swaySpeed * Mathf.PI * 2f + i * 0.6f) * swayAmount;
                float b = Mathf.Cos(t * swaySpeed * 1.3f * Mathf.PI * 2f + i * 0.4f) * swayAmount * 0.5f;
                pivots[i].localRotation = basesRot[i] * Quaternion.Euler(a, 0f, b);
            }

        // Éclairée ou non : transition douce
        bool lit = cristal != null && cristal.ReceivedColor != Colors.None;
        eclairage = Mathf.MoveTowards(eclairage, lit ? 1f : 0f, vitesseTransition * Time.deltaTime);
        float k = Mathf.SmoothStep(0f, 1f, eclairage);

        // Pulsation, plus rapide quand éclairée
        pulseTime += Time.deltaTime * Mathf.Lerp(1f, pulseEclaire, k);
        float p = 1f + Mathf.Sin((pulseTime + phase) * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
        float gain = Mathf.Lerp(1f, lueurEclairee, k);

        if (bulbe != null && block != null)
        {
            bulbe.GetPropertyBlock(block);
            block.SetColor(EmissionId, couleurBulbe * lueurBulbe * p * gain);
            bulbe.SetPropertyBlock(block);
        }
        if (lampe != null) lampe.intensity = lightBase * p * gain;
    }

#if UNITY_EDITOR
    private const string MatFolder = "Assets/Crystals/Plant/Materials";

    [ContextMenu("Coller au sol")]
    private void CollerAuSolMenu()
    {
        Undo.RecordObject(transform, "Coller au sol");
        Physics.SyncTransforms();
        CollerAuSol();
    }

    [ContextMenu("Construire la plante")]
    private void Build()
    {
        Undo.RecordObject(this, "Plante");
        Transform old = transform.Find("Visuel_Plante");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        EnsureFolder(MatFolder);
        Material mTige = Mat("M_Plante_Tige", couleurTige, 0.3f, 0f);
        Material mFeuille = Mat("M_Plante_Feuille", couleurFeuille, 0.45f, 0f);
        Material mPetale = Mat("M_Plante_Petale", couleurPetale, 0.4f, 0f);
        Material mBulbe = Mat("M_Plante_Bulbe", couleurBulbe, 0.8f, lueurBulbe);

        var rootGO = new GameObject("Visuel_Plante");
        Undo.RegisterCreatedObjectUndo(rootGO, "Plante");
        rootGO.layer = gameObject.layer;
        Transform root = rootGO.transform;
        root.SetParent(transform, false);

        // Rosette au sol
        for (int i = 0; i < feuillesSol; i++)
        {
            Transform piv = Empty("Feuille_Sol", root, Vector3.zero,
                                  Quaternion.Euler(0f, i * 360f / feuillesSol + 20f, 0f) * Quaternion.Euler(70f, 0f, 0f));
            Part(PrimitiveType.Sphere, "Feuille", piv, new Vector3(0f, 0.22f * hauteur, 0f), Vector3.zero,
                 new Vector3(0.16f, 0.25f, 0.04f) * hauteur, mFeuille);
        }

        // Tige en segments chaînés
        float segLen = hauteur * 0.85f / segments;
        pivots = new Transform[segments];
        basesRot = new Quaternion[segments];
        Transform parent = root;
        for (int i = 0; i < segments; i++)
        {
            Quaternion rot = Quaternion.Euler(courbure / segments, i == 0 ? 15f : 0f, 0f);
            Transform piv = Empty("Segment_" + i, parent, i == 0 ? Vector3.zero : new Vector3(0f, segLen, 0f), rot);
            float w = Mathf.Lerp(epaisseur, epaisseur * 0.55f, i / (float)Mathf.Max(1, segments - 1)) * hauteur / 1.5f;
            Part(PrimitiveType.Cylinder, "Tige", piv, new Vector3(0f, segLen / 2f, 0f), Vector3.zero,
                 new Vector3(w, segLen / 2f, w), mTige);
            Part(PrimitiveType.Sphere, "Noeud", piv, new Vector3(0f, segLen, 0f), Vector3.zero, Vector3.one * w * 1.1f, mTige);

            // Une feuille sur certains segments, en alternance
            if (i > 0 && i < segments - 1)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Transform fp = Empty("Feuille_Tige", piv, new Vector3(0f, segLen * 0.6f, 0f),
                                     Quaternion.Euler(0f, 90f * side, 0f) * Quaternion.Euler(55f, 0f, 0f));
                Part(PrimitiveType.Sphere, "Feuille", fp, new Vector3(0f, 0.13f * hauteur, 0f), Vector3.zero,
                     new Vector3(0.1f, 0.16f, 0.03f) * hauteur, mFeuille);
            }

            pivots[i] = piv;
            basesRot[i] = rot;
            parent = piv;
        }

        // Tête : pétales + bulbe lumineux
        Transform tete = Empty("Tete", parent, new Vector3(0f, segLen, 0f), Quaternion.identity);
        for (int i = 0; i < petales; i++)
        {
            Transform pp = Empty("Petale", tete, Vector3.zero,
                                 Quaternion.Euler(0f, i * 360f / petales, 0f) * Quaternion.Euler(50f, 0f, 0f));
            Part(PrimitiveType.Sphere, "Petale", pp, new Vector3(0f, tailleBulbe * 0.9f, 0f), Vector3.zero,
                 new Vector3(tailleBulbe * 0.8f, tailleBulbe * 1.3f, tailleBulbe * 0.2f), mPetale);
        }
        bulbe = Part(PrimitiveType.Sphere, "Bulbe", tete, new Vector3(0f, tailleBulbe * 0.6f, 0f), Vector3.zero,
                     new Vector3(tailleBulbe, tailleBulbe * 1.2f, tailleBulbe), mBulbe).GetComponent<Renderer>();

        lampe = null;
        if (lumiere)
        {
            GameObject l = new GameObject("Lumiere_Bulbe");
            l.transform.SetParent(tete, false);
            l.transform.localPosition = new Vector3(0f, tailleBulbe, 0f);
            lampe = l.AddComponent<Light>();
            lampe.type = LightType.Point;
            lampe.color = couleurBulbe;
            lampe.intensity = 0.8f;
            lampe.range = hauteur * 1.5f;
            lampe.shadows = LightShadows.None;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(this);
        Debug.Log("[PlanteLook] Plante construite.");
    }

    private Transform Empty(string n, Transform parent, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject(n);
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        return go.transform;
    }

    private Transform Part(PrimitiveType type, string n, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = n;
        go.layer = gameObject.layer;
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    private static Material Mat(string n, Color c, float smooth, float emission)
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