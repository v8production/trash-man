using UnityEngine;

public static class TitanGroundFrame
{
    public static Vector3 Up
    {
        get
        {
            Vector3 gravity = Physics.gravity;
            return gravity.sqrMagnitude > 0.0001f ? -gravity.normalized : Vector3.up;
        }
    }

    public static Vector3 WorldForward => ProjectWorldAxis(Vector3.forward, Vector3.right);
    public static Vector3 WorldRight => ProjectWorldAxis(Vector3.right, Vector3.forward);

    public static Quaternion GroundRotation(Vector3 forward)
    {
        Vector3 up = Up;
        Vector3 planarForward = Vector3.ProjectOnPlane(forward, up);
        if (planarForward.sqrMagnitude < 0.0001f)
            planarForward = WorldForward;

        return Quaternion.LookRotation(planarForward.normalized, up);
    }

    private static Vector3 ProjectWorldAxis(Vector3 axis, Vector3 fallback)
    {
        Vector3 projected = Vector3.ProjectOnPlane(axis, Up);
        if (projected.sqrMagnitude < 0.0001f)
            projected = Vector3.ProjectOnPlane(fallback, Up);

        return projected.normalized;
    }
}
