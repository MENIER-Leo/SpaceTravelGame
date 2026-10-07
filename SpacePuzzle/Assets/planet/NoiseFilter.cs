using UnityEngine;

public class NoiseFilter
{
    int seed;

    readonly NoiseSettings settings;

    Vector3 offset;

    public NoiseFilter(int seed, NoiseSettings settings)
    {
        this.seed = seed;
        this.settings = settings;

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
        float frequency = settings.baseFrequency;
        float amplitude = 1f;
        float totalAmplitude = 0f;
        Vector3 pointF;

        for (int i = 0; i < settings.layers; i++)
        {
            pointF = pointOnUnitSphere * frequency + offset;
            noiseValue += Unity.Mathematics.noise.snoise(new Unity.Mathematics.float3(pointF)) * amplitude;
            frequency *= settings.roughness;
            totalAmplitude += amplitude;
            amplitude *= settings.persistence;
        }
        return noiseValue/totalAmplitude;
    }
}
