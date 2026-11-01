using UnityEngine;

// Outils de placement sur un terrain de planète (relief procédural).
public static class TerrainSnap
{
    private static readonly RaycastHit[] buffer = new RaycastHit[16];

    // Distance au centre de la surface la plus haute de 'planet' dans la direction 'dir'.
    // Seuls les colliders enfants de 'planet' comptent (ignore les autres planètes).
    public static bool GroundRadius(Transform planet, Vector3 dir, float startRadius, int mask,
                                    out float radius, out Vector3 point)
    {
        Vector3 center = planet.position;
        int count = Physics.RaycastNonAlloc(center + dir * startRadius, -dir, buffer, startRadius,
                                            mask, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        point = Vector3.zero;
        radius = 0f;

        for (int i = 0; i < count; i++)
        {
            if (!buffer[i].collider.transform.IsChildOf(planet)) continue;
            if (buffer[i].distance < best)
            {
                best = buffer[i].distance;
                point = buffer[i].point;
            }
        }

        if (best == float.MaxValue) return false;
        radius = startRadius - best;
        return true;
    }

    // Pose 't' sur le sol, debout selon la verticale de la planète. Conserve le cap.
    public static bool Snap(Transform t, Transform planet, int mask)
    {
        Vector3 offset = t.position - planet.position;
        Vector3 up = offset.normalized;
        if (up == Vector3.zero) return false;

        Physics.SyncTransforms();
        if (!GroundRadius(planet, up, offset.magnitude + 50f, mask, out _, out Vector3 ground))
            return false;

        float pivotHeight = 0f;
        BoxCollider box = t.GetComponent<BoxCollider>();
        if (box != null) pivotHeight = (box.size.y * 0.5f - box.center.y) * t.lossyScale.y;

        t.position = ground + up * pivotHeight;
        t.rotation = Quaternion.FromToRotation(t.up, up) * t.rotation;
        return true;
    }
}
