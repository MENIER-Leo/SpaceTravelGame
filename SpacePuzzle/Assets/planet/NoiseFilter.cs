using UnityEngine;

public class NoiseFilter
{
    int seed;

    readonly int layers;
    readonly float roughness;
    readonly float strength;
    readonly float baseFrequency;

    Vector3 offset;

    public NoiseFilter(int seed, int layers,float baseFrequency, float roughness, float strength)
    {
        this.seed = seed;
        this.layers = layers;
        this.roughness = roughness;
        this.strength = strength;
        this.baseFrequency = baseFrequency;

        GenerateSeed();
    }

    void GenerateSeed()
    {
        System.Random random = new System.Random(seed);
        offset = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()) * 100f;

    }

    public float Evaluate(Vector3 pointOnUnitSphere)
    {

        float noiseValue = 0f;
        float frequency = baseFrequency;
        float amplitude = 1f;
        float totalAmplitude = 0f;
        Vector3 pointF;

        for (int i = 0; i < layers; i++)
        {
            pointF = pointOnUnitSphere * frequency + offset;
            noiseValue += Unity.Mathematics.noise.snoise(new Unity.Mathematics.float3(pointF)) * amplitude;
            frequency *= this.roughness;
            totalAmplitude += amplitude;
            amplitude *= this.strength;
        }
        return noiseValue/totalAmplitude;
    }
}
