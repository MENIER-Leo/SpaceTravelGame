using UnityEngine;

public class Planet : MonoBehaviour
{
    [Range(2, 128)]
    public int resolution = 10;

    public PlanetSettings settings;
    PlanetGenerator planetGenerator;

    public Material planetMaterial;

    [SerializeField, HideInInspector]
    MeshFilter[] meshFilters;
    PlanetFace[] faces;

    MinMax distanceMinMax;
    public MinMax DistanceMinMax => distanceMinMax;
    static readonly int MinMaxId = Shader.PropertyToID("_MinMax");
    MaterialPropertyBlock propertyBlock;

    void Initialize()
    {
        if (meshFilters == null || meshFilters.Length == 0)
            meshFilters = new MeshFilter[6];

        faces = new PlanetFace[6];

        Vector3[] dir = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        
        planetGenerator = new PlanetGenerator(settings);

        distanceMinMax = new MinMax();

        for (int i = 0; i < 6; i++)
        {
            if (meshFilters[i] == null)
            {
                GameObject meshObj = new GameObject("mesh");
                meshObj.layer = gameObject.layer;          // on utilise meshObj, pas meshFilters[i]
                meshObj.transform.parent = transform;
                meshFilters[i] = meshObj.AddComponent<MeshFilter>();
            }

            MeshRenderer rend = meshFilters[i].GetComponent<MeshRenderer>();
            if (rend == null)
                rend = meshFilters[i].gameObject.AddComponent<MeshRenderer>();
            rend.sharedMaterial = planetMaterial;

            MeshCollider col = meshFilters[i].GetComponent<MeshCollider>();
            if (col == null)
                col = meshFilters[i].gameObject.AddComponent<MeshCollider>();

            if (meshFilters[i].sharedMesh == null)
                meshFilters[i].sharedMesh = new Mesh();

            faces[i] = new PlanetFace(dir[i], resolution, meshFilters[i].sharedMesh, planetGenerator, distanceMinMax);
        }
    }


    void GenerateMesh()
    {
        for (int i = 0; i < 6; i++)
        {
            faces[i].mesh.Clear();
            faces[i].BuildMesh();

            MeshCollider col = meshFilters[i].GetComponent<MeshCollider>();
            col.sharedMesh = null;               // force la reconstruction de la collision
            col.sharedMesh = faces[i].mesh;
        }

        UpdateShaderMinMax();
    }

    void OnValidate()
    {
        if (settings == null) return;

        settings.onChanged -= Regenerate;   // d'abord on se désabonne...
        settings.onChanged += Regenerate;   // ...puis on s'abonne (évite les doublons)

        Regenerate();
    }

    private void Start()
    {
        Initialize();
        GenerateMesh();
    }

    [ContextMenu("Regenerate")]
    public void Regenerate()
    {
        if (settings == null) return;
        Initialize();
        GenerateMesh();
    }

    void UpdateShaderMinMax()
    {
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        Vector4 minMax = new Vector4(distanceMinMax.Min, distanceMinMax.Max, 0f, 0f);

        for (int i = 0; i < 6; i++)
        {
            MeshRenderer rend = meshFilters[i].GetComponent<MeshRenderer>();
            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(MinMaxId, minMax);
            rend.SetPropertyBlock(propertyBlock);
        }
    }
}