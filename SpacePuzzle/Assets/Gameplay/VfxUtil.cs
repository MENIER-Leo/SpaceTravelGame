using UnityEngine;

/// Petits effets de particules générés par code (URP).
/// Tous se détruisent seuls une fois terminés.
public static class VfxUtil
{
    // ---------- Base ----------

    public static ParticleSystem Create(string name, Vector3 pos, Quaternion rot, Material mat, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetPositionAndRotation(pos, rot);
        if (parent != null) go.transform.SetParent(parent, true);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.gravityModifier = 0f;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = false;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = mat;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    public static void FadeOut(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    public static void ShrinkOut(ParticleSystem ps)
    {
        var s = ps.sizeOverLifetime;
        s.enabled = true;
        s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
    }

    private static void Burst(ParticleSystem ps, int count)
    {
        var em = ps.emission;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    // ---------- Effets prêts à l'emploi ----------

    /// Anneau de lumière qui s'étale à plat autour de "up".
    public static void Ring(Vector3 pos, Vector3 up, Color color, Material mat,
                            float speed = 6f, int count = 48, float size = 0.3f)
    {
        var ps = Create("VFX_Anneau", pos, Quaternion.FromToRotation(Vector3.forward, up), mat);
        var main = ps.main;
        main.duration = 1f;
        main.startLifetime = 0.6f;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;
        shape.radiusThickness = 0f;

        Burst(ps, count);
        FadeOut(ps);
        ps.Play();
    }

    /// Gerbe d'étincelles projetées vers "up".
    public static void Sparkles(Vector3 pos, Vector3 up, Color color, Material mat,
                                int count = 30, float minSpeed = 2f, float maxSpeed = 5f, float angle = 30f)
    {
        var ps = Create("VFX_Etincelles", pos, Quaternion.FromToRotation(Vector3.forward, up), mat);
        var main = ps.main;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = color;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = angle;
        shape.radius = 0.3f;

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0.5f;
        limit.dampen = 0.08f;

        Burst(ps, count);
        FadeOut(ps);
        ShrinkOut(ps);
        ps.Play();
    }

    /// Grosse lueur unique qui disparaît vite (flash).
    public static void Glow(Vector3 pos, Color color, Material mat, float size = 3f, float life = 0.4f)
    {
        var ps = Create("VFX_Lueur", pos, Quaternion.identity, mat);
        var main = ps.main;
        main.duration = life;
        main.startLifetime = life;
        main.startSpeed = 0f;
        main.startSize = size;
        main.startColor = color;

        Burst(ps, 1);
        FadeOut(ps);
        ps.Play();
    }

    /// Traînée d'étincelles qui suit un objet pendant "duration" secondes.
    public static void Trail(Transform target, Color color, Material mat, float duration = 1.5f, float rate = 40f)
    {
        var ps = Create("VFX_Trainee", target.position, target.rotation, mat, target);
        var main = ps.main;
        main.duration = duration;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = color;

        var em = ps.emission;
        em.rateOverTime = rate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        FadeOut(ps);
        ShrinkOut(ps);
        ps.Play();
    }
}
