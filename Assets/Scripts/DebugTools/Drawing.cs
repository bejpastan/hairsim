using UnityEngine;

public static class Drawing
{
    private static int circleSegmentCount = 36;

    public static void DrawSphereoid(Vector3 position, Vector3 scale, Color color, Quaternion quaternion, float duration = 1)
    {
        Quaternion up = quaternion * Quaternion.Euler(90, 0, 0);
        Quaternion right = quaternion * Quaternion.Euler(0, 0, 90);
        Quaternion forward = quaternion;

        DrawCircle(position, up, new Vector2(scale.x, scale.y), color, duration);
        DrawCircle(position, right, new Vector2(scale.y, scale.z), color, duration);
        DrawCircle(position, forward, new Vector2(scale.x, scale.z), color, duration);
    }

    public static void DrawCircle(Vector3 position, Quaternion rotation, Vector2 radius, Color color, float duration = 1)
    {
        for (int i = 0; i < circleSegmentCount; i++)
        {
            float angleA = (i / (float)circleSegmentCount) * Mathf.PI * 2f;
            float angleB = ((i + 1) / (float)circleSegmentCount) * Mathf.PI * 2f;
            Vector3 pointA = position + rotation * new Vector3(Mathf.Cos(angleA) * radius.x, 0, Mathf.Sin(angleA) * radius.y);
            Vector3 pointB = position + rotation * new Vector3(Mathf.Cos(angleB) * radius.x, 0, Mathf.Sin(angleB) * radius.y);
            Debug.DrawLine(pointA, pointB, color, duration);
        }
    }
}
