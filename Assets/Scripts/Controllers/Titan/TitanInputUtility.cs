using UnityEngine;

public static class TitanInputUtility
{
    public static Vector2 KeepDominantAxis(Vector2 value, float deadZone = 0.01f)
    {
        float absX = Mathf.Abs(value.x);
        float absY = Mathf.Abs(value.y);

        if (absX < deadZone && absY < deadZone)
        {
            return Vector2.zero;
        }

        if (absX >= absY)
        {
            return new Vector2(value.x, 0f);
        }

        return new Vector2(0f, value.y);
    }

    public static Vector2 KeepDominantAxisWithHorizontalBias(Vector2 value, float deadZone, float horizontalBiasPixels)
    {
        float absX = Mathf.Abs(value.x);
        float absY = Mathf.Abs(value.y);

        if (absX < deadZone && absY < deadZone)
            return Vector2.zero;

        if ((absX + Mathf.Max(0f, horizontalBiasPixels)) >= absY)
            return new Vector2(value.x, 0f);

        return new Vector2(0f, value.y);
    }

    public static Vector2 ComputeSphericalAngles(
        Vector2 mousePosition,
        Vector2 origin,
        float thetaRadiusPixels,
        float maxThetaDegrees,
        float sensitivity,
        float secondaryMaxDegrees = -1f,
        bool applySensitivityToSecondary = true)
    {
        Vector2 fromOrigin = mousePosition - origin;
        float radius = fromOrigin.magnitude;
        float theta01 = thetaRadiusPixels > 0f ? Mathf.Clamp01(radius / thetaRadiusPixels) : 0f;
        float theta = theta01 * maxThetaDegrees * sensitivity;
        float phiRad = Mathf.Atan2(fromOrigin.y, fromOrigin.x);
        float normalizedY = thetaRadiusPixels > 0f ? Mathf.Clamp(fromOrigin.y / thetaRadiusPixels, -1f, 1f) : 0f;

        float yaw = theta * Mathf.Cos(phiRad);
        float resolvedSecondaryMax = secondaryMaxDegrees > 0f ? secondaryMaxDegrees : maxThetaDegrees;
        float secondaryScale = applySensitivityToSecondary ? sensitivity : 1f;
        float pitchOrRoll = -(resolvedSecondaryMax * secondaryScale) * normalizedY;
        return new Vector2(yaw, pitchOrRoll);
    }

}
