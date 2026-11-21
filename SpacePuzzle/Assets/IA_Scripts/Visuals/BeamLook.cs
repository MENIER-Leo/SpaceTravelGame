using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

/// Look des faisceaux. À placer sur le prefab BeamSegment (LineRenderer).
/// BeamManager place les points et envoie la couleur via _EmissionColor ; ce script fait le rendu.
[RequireComponent(typeof(LineRenderer))]
public class BeamLook : MonoBehaviour
{
    [Header("Matériaux")]
    [SerializeField, Tooltip("Particles/Unlit additif, Base Map = T_Faisceau_Energie.")]
    private Material materiauCoeur;
    [SerializeField, Tooltip("Particles/Unlit additif, Base Map = T_Faisceau_Halo.")]
    private Material materiauHalo;
    [SerializeField, Tooltip("M_Particule_Lumiere. Vide = pas d'étincelles.")]
    private Material materiauParticules;

    [Header("Coeur")]
    [SerializeField] private float coreWidth = 0.07f;
    [SerializeField] private float coreIntensity = 3f;
    [SerializeField, Range(0f, 1f), Tooltip("Mélange vers le blanc : coeur plus « chaud ».")]
    private float coreWhiteness = 0.45f;
    [SerializeField, Tooltip("Vitesse de défilement de l'énergie (vers la cible).")]
    private float scrollSpeed = 2.5f;
    [SerializeField, Tooltip("Nombre de motifs d'énergie par mètre.")]
    private float tiling = 1.2f;

    [Header("Halo")]
    [SerializeField] private float haloWidth = 0.4f;
    [SerializeField] private float haloIntensity = 1.2f;
    [SerializeField] private float pulseAmount = 0.15f;
    [SerializeField] private float pulseSpeed = 1.2f;

    [Header("Étincelles")]
    [SerializeField, Tooltip("Étincelles par mètre de faisceau et par seconde.")]
    private float sparkRate = 6f;
    [SerializeField] private float sparkSpeed = 2.5f;
    [SerializeField] private float sparkSize = 0.05f;
    [SerializeField] private float sparkLife = 0.6f;
    [SerializeField] private float sparkJitter = 0.04f;

    [Header("Impact")]
    [SerializeField] private float impactRate = 18f;
    [SerializeField] private float impactSize = 0.45f;
    [SerializeField] private float impactLife = 0.25f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    private LineRenderer core, halo;
    private ParticleSystem particles;
    private MaterialPropertyBlock coreBlock, haloBlock;
    private Vector3[] points = new Vector3[64];
    private float[] cumul = new float[64];
    private int count;
    private float totalLength, sparkAcc, impactAcc, phase;

    private void Awake()
    {
        core = GetComponent<LineRenderer>();
        if (materiauCoeur != null) core.sharedMaterial = materiauCoeur;
        core.textureMode = LineTextureMode.Tile;
        core.textureScale = new Vector2(tiling, 1f);
        core.widthMultiplier = coreWidth;
        core.numCapVertices = 4;
        core.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        coreBlock = new MaterialPropertyBlock();
        haloBlock = new MaterialPropertyBlock();
        phase = Random.value * 10f;

        CreateHalo();
        CreateParticles();
    }

    private void OnEnable()
    {
        sparkAcc = 0f;
        impactAcc = 0f;
    }

    private void CreateHalo()
    {
        if (materiauHalo == null) return;
        var go = new GameObject("Halo");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        halo = go.AddComponent<LineRenderer>();
        halo.sharedMaterial = materiauHalo;
        halo.useWorldSpace = true;
        halo.alignment = LineAlignment.View;
        halo.textureMode = LineTextureMode.Stretch;
        halo.numCapVertices = 6;
        halo.numCornerVertices = 2;
        halo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        halo.receiveShadows = false;
        halo.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        halo.positionCount = 0;
    }

    private void CreateParticles()
    {
        if (materiauParticules == null) return;
        var go = new GameObject("Particules_Faisceau");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1000;
        main.startSpeed = 0f;
        main.startLifetime = sparkLife;
        main.startSize = sparkSize;

        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;

        var sol = particles.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var col = particles.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = materiauParticules;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        particles.Play();
    }

    private void LateUpdate()
    {
        ReadPoints();
        if (count < 2)
        {
            if (halo != null) halo.positionCount = 0;
            return;
        }

        // Couleur envoyée par BeamManager (normalisée)
        core.GetPropertyBlock(coreBlock);
        Color e = coreBlock.GetColor(EmissionColorId);
        float max = Mathf.Max(e.r, Mathf.Max(e.g, e.b));
        Color c = max > 0.0001f ? new Color(e.r / max, e.g / max, e.b / max, 1f) : Color.white;

        float t = Time.time + phase;
        float pulse = 1f + Mathf.Sin(t * pulseSpeed * Mathf.PI * 2f) * pulseAmount;

        // Coeur : couleur HDR + défilement de la texture
        Color coreColor = Color.Lerp(c, Color.white, coreWhiteness) * coreIntensity;
        coreColor.a = 1f;
        coreBlock.SetColor(BaseColorId, coreColor);
        coreBlock.SetVector(BaseMapStId, new Vector4(1f, 1f, -t * scrollSpeed * tiling, 0f));
        core.SetPropertyBlock(coreBlock);
        core.widthMultiplier = coreWidth;

        // Halo
        if (halo != null)
        {
            halo.positionCount = count;
            halo.SetPositions(points);
            halo.widthMultiplier = haloWidth * pulse;
            Color hc = c * haloIntensity * pulse;
            hc.a = 1f;
            haloBlock.SetColor(BaseColorId, hc);
            halo.SetPropertyBlock(haloBlock);
        }

        EmitParticles(c);
    }

    private void ReadPoints()
    {
        count = core.positionCount;
        if (points.Length < count)
        {
            points = new Vector3[count * 2];
            cumul = new float[count * 2];
        }
        core.GetPositions(points);

        totalLength = 0f;
        if (count > 0) cumul[0] = 0f;
        for (int i = 1; i < count; i++)
        {
            totalLength += Vector3.Distance(points[i - 1], points[i]);
            cumul[i] = totalLength;
        }
    }

    private void EmitParticles(Color c)
    {
        if (particles == null || totalLength <= 0.01f) return;
        float dt = Time.deltaTime;
        Color32 col = c;

        // Étincelles le long du faisceau
        sparkAcc += sparkRate * totalLength * dt;
        while (sparkAcc >= 1f)
        {
            sparkAcc -= 1f;
            float d = Random.value * totalLength;
            int i = 1;
            while (i < count - 1 && cumul[i] < d) i++;
            float segLen = cumul[i] - cumul[i - 1];
            float k = segLen > 0f ? (d - cumul[i - 1]) / segLen : 0f;
            Vector3 dir = (points[i] - points[i - 1]).normalized;

            var p = new ParticleSystem.EmitParams
            {
                position = Vector3.Lerp(points[i - 1], points[i], k) + Random.insideUnitSphere * sparkJitter,
                velocity = dir * sparkSpeed * Random.Range(0.7f, 1.3f) + Random.insideUnitSphere * 0.2f,
                startColor = col,
                startSize = sparkSize * Random.Range(0.7f, 1.3f),
                startLifetime = sparkLife * Random.Range(0.7f, 1.2f),
                applyShapeToPosition = false
            };
            particles.Emit(p, 1);
        }

        // Éclat à l'impact
        impactAcc += impactRate * dt;
        while (impactAcc >= 1f)
        {
            impactAcc -= 1f;
            var p = new ParticleSystem.EmitParams
            {
                position = points[count - 1],
                velocity = Vector3.zero,
                startColor = col,
                startSize = impactSize * Random.Range(0.8f, 1.2f),
                startLifetime = impactLife,
                applyShapeToPosition = false
            };
            particles.Emit(p, 1);
        }
    }

#if UNITY_EDITOR
    private const string TexFolder = "Assets/VFX/Textures";

    [ContextMenu("Générer les textures")]
    private void GenerateTextures()
    {
        EnsureFolder(TexFolder);

        // Énergie : coeur net en travers, pulsations le long (répétable en U)
        Texture2D energie = MakeTexture("T_Faisceau_Energie", 128, 32, true, (u, v) =>
        {
            float across = Gauss(v, 0.5f, 0.22f);
            float wave = Mathf.Sin(u * Mathf.PI * 2f) * 0.5f + 0.5f;
            float along = 0.55f + 0.45f * Mathf.Pow(wave, 3f) + 0.12f * Mathf.Sin(u * Mathf.PI * 6f + 1.3f);
            return Mathf.Clamp01(across * along * 1.1f);
        });

        // Halo : dégradé doux en travers
        Texture2D haloTex = MakeTexture("T_Faisceau_Halo", 32, 64, false, (u, v) => Gauss(v, 0.5f, 0.2f));

        if (materiauCoeur != null)
        {
            Undo.RecordObject(materiauCoeur, "Textures faisceau");
            materiauCoeur.SetTexture("_BaseMap", energie);
            EditorUtility.SetDirty(materiauCoeur);
        }
        if (materiauHalo != null)
        {
            Undo.RecordObject(materiauHalo, "Textures faisceau");
            materiauHalo.SetTexture("_BaseMap", haloTex);
            EditorUtility.SetDirty(materiauHalo);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[BeamLook] Textures générées et assignées.");
    }

    private static float Gauss(float x, float m, float s)
    {
        float d = (x - m) / s;
        return Mathf.Exp(-d * d);
    }

    private static Texture2D MakeTexture(string name, int w, int h, bool repeatU, System.Func<float, float, float> f)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = f(x / (float)w, (y + 0.5f) / h);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();

        string path = $"{TexFolder}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = true;
        imp.wrapModeU = repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.wrapModeV = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
