using UnityEngine;

public class Planet : MonoBehaviour
{
    [Range(2, 128)]
    public int resolution = 10;

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

            faces[i] = new PlanetFace(dir[i], resolution, meshFilters[i].sharedMesh);
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