using UnityEngine;
using UnityEngine.InputSystem;

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

    public static Vector2 ReadMousePosition()
    {
        return Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : Vector2.zero;
    }

    public static float GetAxis(
        Key positive,
        Key negative)
    {
        float axis = 0f;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return 0f;

        if (keyboard[positive].isPressed)
            axis += 1f;

        if (keyboard[negative].isPressed)
            axis -= 1f;

        return axis;
    }

    public static bool WasDigitPressedThisFrame(int digitOneToFive)
    {
        switch (digitOneToFive)
        {
            case 1:
                return WasPressedThisFrame(Key.Digit1);
            case 2:
                return WasPressedThisFrame(Key.Digit2);
            case 3:
                return WasPressedThisFrame(Key.Digit3);
            case 4:
                return WasPressedThisFrame(Key.Digit4);
            case 5:
                return WasPressedThisFrame(Key.Digit5);
            default:
                return false;
        }
    }

    private static bool WasPressedThisFrame(Key key)
    {
        return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    }
}
