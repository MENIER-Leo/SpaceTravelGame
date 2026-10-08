using UnityEngine;

public class PlanetGenerator
{

    readonly PlanetSettings settings;
    readonly NoiseFilter continents;
    readonly NoiseFilter mountains;
    readonly NoiseFilter craters;

    public PlanetGenerator(PlanetSettings settings)
    {
        this.settings = settings;
        this.continents = new NoiseFilter(settings.seed, settings.continents);
        this.mountains = new NoiseFilter(settings.seed + 1, settings.mountains);
        this.craters = new NoiseFilter(settings.seed + 2, settings.craters);
    }


    public Vector3 calculatePoint(Vector3 pointOnUnitSphere)
    {
        PlanetSettings s = settings;

        float continentNoise = continents.Evaluate(pointOnUnitSphere);

        float land = Mathf.SmoothStep(0f, 1f, continentNoise - s.seaLevel) / s.seaRelief;

        float crater = Mathf.InverseLerp(s.mountainZoneStart, s.mountainZoneEnd, craters.Evaluate(pointOnUnitSphere));

        float hills = craters.Evaluate(pointOnUnitSphere) * 0.5f + 0.5f;

        float relief = Mathf.Lerp(s.plainsRoughness, s.mountainHeight, crater);

        float height = land * (s.plainsHeight + hills * relief);

        return pointOnUnitSphere * s.radius * (1f + height);
    }
}
