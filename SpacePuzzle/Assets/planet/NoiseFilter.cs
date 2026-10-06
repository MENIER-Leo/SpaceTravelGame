using UnityEngine;

public class NoiseFilter
{
    int seed;

    public int layers;
    public float roughness;
    public float strength;

    Vector3 offset;

    public NoiseFilter(int seed, int layers, float roughness, float strength)
    {
        this.seed = seed;
        this.layers = layers;
        this.roughness = roughness;
        this.strength = strength;
    }

    void GenerateSeed()
    {
        System.Random random = new System.Random(seed);
        offset = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());

    }

    public float Evaluate(Vector3 pointOnUnitSphere)
    {
        GenerateSeed();

        float noiseValue = 0;
        float frequency = 1;
        float amplitude = 1;
        float totalAmplitude = 0;
        Vector3 pointF;

        for (int i = 0; i < layers; i++)
        {
            pointF = pointOnUnitSphere * frequency + offset;
            noiseValue += Unity.Mathematics.noise.snoise(new Unity.Mathematics.float3(pointF)) * amplitude;
            frequency *= this.roughness;
            amplitude *= this.strength;
            totalAmplitude += amplitude;
        }
        return noiseValue/totalAmplitude;
    }
}
