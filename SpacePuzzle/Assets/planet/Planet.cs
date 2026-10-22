using UnityEngine;

public class Planet : MonoBehaviour
{
    [Range(2, 128)]
    public int resolution = 10;

    public PlanetSettings settings;
    PlanetGenerator planetGenerator;

    public Material planetMaterial;

    MinMax distanceMinMax;

    [SerializeField, HideInInspector]
    MeshFilter[] meshFilters;
    PlanetFace[] faces;

    void Initialize()
    {
        if (meshFilters == null || meshFilters.Length == 0)
            meshFilters = new MeshFilter[6];

        faces = new PlanetFace[6];

        distanceMinMax = new MinMax();

        Vector3[] dir = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        
        planetGenerator = new PlanetGenerator(settings);

        for (int i = 0; i < 6; i++)
        {
            if (meshFilters[i] == null)
            {
                GameObject meshObj = new GameObject("mesh");
                meshObj.layer = gameObject.layer;
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

        if (planetMaterial != null)
            planetMaterial.SetVector("_MinMax", new Vector2(distanceMinMax.Min, distanceMinMax.Max));
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
}