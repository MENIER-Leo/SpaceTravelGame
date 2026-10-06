using UnityEngine;

public class Planet : MonoBehaviour
{
    [Range(2, 128)]
    public int resolution = 10;

    [Range (1f, 10f)]
    public float radius = 1f;
    [Range(0f, 1f)]
    public float floor = 1f;
    [SerializeField]
    public int seed = 1;



    [Range(1, 8)]
    public int layers = 4;
    [Range(1f, 10f)]
    public float roughness = 4f;
    [Range(0.1f, 1f)]
    public float strength = 0.25f;
    [Range(0.1f, 1f)]
    public float baseFrequency = 0.5f;

    public NoiseFilter noiseFilter;

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


        for (int i = 0; i < 6; i++)
        {
            if (meshFilters[i] == null)
            {
                GameObject meshObj = new GameObject("mesh");
                meshObj.transform.parent = transform;
                meshObj.AddComponent<MeshRenderer>().sharedMaterial = planetMaterial;
                meshFilters[i] = meshObj.AddComponent<MeshFilter>();
                meshFilters[i].sharedMesh = new Mesh();
            }
            else
            {
                meshFilters[i].GetComponent<MeshRenderer>().sharedMaterial = planetMaterial;
            }

            noiseFilter = new NoiseFilter(seed, layers, baseFrequency, roughness, strength);
            faces[i] = new PlanetFace(dir[i], resolution, meshFilters[i].sharedMesh, radius, noiseFilter, floor);
        }
    }


    void GenerateMesh()
    {
        foreach (PlanetFace face in faces)
        {
            face.mesh.Clear();
            face.buildMesh();
        }
    }

    private void OnValidate()
    {
        Initialize();
        GenerateMesh();
    }
}