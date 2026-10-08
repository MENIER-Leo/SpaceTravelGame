using UnityEngine;


[System.Serializable]
public class NoiseSettings
{
    [Range(1, 8)]
    public int layers = 3;
    [Range(1f, 10f)]
    public float roughness = 2f;
    [Range(0.1f, 1f)]
    public float persistence = 0.5f;
    [Range(0.1f, 1f)]
    public float baseFrequency = 1f;
}


[CreateAssetMenu(menuName = "Planet/Planet Settings")]
public class PlanetSettings : ScriptableObject
{

    [Range(1f, 10f)]
    public float radius = 1f;
    
    public int seed = 1;

    public NoiseSettings continents = new NoiseSettings { layers = 4, baseFrequency = 0.8f };

    [Range(-0.5f, 0.5f)]
    public float seaLevel = 0f;
    [Range(0.01f, 0.5f)]
    public float seaRelief = 0.1f;

    public NoiseSettings mountains = new NoiseSettings { layers = 5, baseFrequency = 1.5f, roughness = 2f, persistence = 0.5f };

    [Range(-1f, 1f)] 
    public float mountainZoneStart = 0.1f;
    [Range(-1f, 1f)] 
    public float mountainZoneEnd = 0.5f;

    public NoiseSettings craters = new NoiseSettings { layers = 3, baseFrequency = 2f, roughness = 1.5f, persistence = 0.5f };

    [Range(0f, 0.2f)] 
    public float plainsHeight = 0.01f;    
    [Range(0f, 0.2f)] 
    public float plainsRoughness = 0.005f; 
    [Range(0f, 0.5f)] 
    public float mountainHeight = 0.06f;


    public event System.Action onChanged;

    void OnValidate()
    {
        onChanged?.Invoke();
    }

}
