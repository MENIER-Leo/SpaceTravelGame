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

    void Initialize()
    {
        if (meshFilters == null || meshFilters.Length == 0)
            meshFilters = new MeshFilter[6];

        faces = new PlanetFace[6];

        Vector3[] dir = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        
        planetGenerator = new PlanetGenerator(settings);

        for (int i = 0; i < 6; i++)
        {
            if (meshFilters[i] == null)
            {
                GameObject meshObj = new GameObject("mesh");
                meshObj.transform.parent = transform;
                meshFilters[i] = meshObj.AddComponent<MeshFilter>();
            }


            MeshRenderer rend = meshFilters[i].GetComponent<MeshRenderer>();
            if (rend == null)
                rend = meshFilters[i].gameObject.AddComponent<MeshRenderer>();
            rend.sharedMaterial = planetMaterial;

            if (meshFilters[i].sharedMesh == null)
                meshFilters[i].sharedMesh = new Mesh();

            faces[i] = new PlanetFace(dir[i], resolution, meshFilters[i].sharedMesh, planetGenerator);
        }
    }


    void GenerateMesh()
    {
        foreach (PlanetFace face in faces)
        {
            face.mesh.Clear();
            face.BuildMesh();
        }
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