using UnityEngine;

public class PlanetFace
{

    public Mesh mesh;
    public int resolution;

    Vector3 localUp;
    Vector3 AxisA;
    Vector3 AxisB;


    private Vector3[] vertices;
    private int[] triangle;


    public PlanetFace(Vector3 localUp, int resolution, Mesh mesh)
    {
        this.localUp = localUp;
        AxisA = new Vector3(this.localUp.y, this.localUp.z, this.localUp.x);
        AxisB = Vector3.Cross(this.localUp, AxisA);
        this.mesh = mesh;
        this.resolution = resolution;   
    }

    public void buildMesh()
    {
        addPoints();
        addTriangles();

        this.mesh.Clear();
        this.mesh.vertices = vertices;
        this.mesh.triangles = triangle;

        this.mesh.RecalculateNormals();
    }

    void addPoints()
    {
        vertices = new Vector3[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            for(int i = 0; i< resolution; i++)
            {
                int index = i + y * resolution;
                Vector2 percent = new Vector2(i, y) / (resolution - 1);
                Vector3 pointOnUnitCube = localUp + (percent.x - 0.5f) * 2 * AxisA + (percent.y - 0.5f) * 2 * AxisB;
                Vector3 pointOnUnitSphere = pointOnUnitCube.normalized;
                vertices[index] = pointOnUnitSphere;
            }
        }
    }


    void addTriangles()
    {
        triangle = new int[(resolution - 1) * (resolution - 1) * 6];
        int triIndex = 0;
        for(int y = 0; y < resolution - 1; y++)
        {
            for(int i = 0; i < resolution - 1; i++)
            {
                int index = i + y * resolution;
                triangle[triIndex] = index;
                triangle[triIndex + 1] = index + resolution + 1;
                triangle[triIndex + 2] = index + resolution;
                triangle[triIndex + 3] = index;
                triangle[triIndex + 4] = index + 1;
                triangle[triIndex + 5] = index + resolution + 1;
                triIndex += 6;
            }
        }
    }
}
