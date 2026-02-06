using UnityEngine;

public static class Drawing
{
    private static int circleSegmentCount = 36;


    /// <summary>
    /// 
    /// </summary>
    /// <param name="position"></param>
    /// <param name="scale">diameters</param>
    /// <param name="color"></param>
    /// <param name="quaternion"></param>
    /// <param name="duration"></param>
    public static void DrawSphereoid(Vector3 position, Vector3 scale, Color color, Quaternion quaternion, float duration = 1)
    {
        Quaternion up = quaternion * Quaternion.Euler(90, 0, 0);
        Quaternion right = quaternion * Quaternion.Euler(0, 0, 90);
        Quaternion forward = quaternion;
        scale /= 2;

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

    public static void DrawCube(Vector3 position, Vector3 sizes, Color color, float duration = 1)
    { 
        Debug.DrawLine(position + new Vector3(-sizes.x, -sizes.y, -sizes.z) / 2, position + new Vector3(sizes.x, -sizes.y, -sizes.z) / 2, color, duration);
        Debug.DrawLine(position + new Vector3(-sizes.x, -sizes.y, -sizes.z) / 2, position + new Vector3(-sizes.x, sizes.y, -sizes.z) / 2, color, duration);
        Debug.DrawLine(position + new Vector3(-sizes.x, -sizes.y, -sizes.z) / 2, position + new Vector3(-sizes.x, -sizes.y, sizes.z) / 2, color, duration);
        Debug.DrawLine(position + new Vector3(sizes.x, sizes.y, sizes.z) / 2, position + new Vector3(-sizes.x, sizes.y, sizes.z) / 2, color, duration);
        Debug.DrawLine(position + new Vector3(sizes.x, sizes.y, sizes.z) / 2, position + new Vector3(sizes.x, -sizes.y, sizes.z) / 2, color, duration);
        Debug.DrawLine(position + new Vector3(sizes.x, sizes.y, sizes.z) / 2, position + new Vector3(sizes.x, sizes.y, -sizes.z) / 2, color, duration);
        Debug.DrawLine(position + new Vector3(-sizes.x, sizes.y, -sizes.z) / 2, position + new Vector3(sizes.x, sizes.y, -sizes.z) / 2, color, duration);
        Debug.DrawLine(position + new Vector3(-sizes.x, sizes.y, -sizes.z) / 2, position + new Vector3(-sizes.x, sizes.y, sizes.z) / 2, color, duration);
        Debug.DrawLine(position + new Vector3(sizes.x, -sizes.y, -sizes.z) / 2, position + new Vector3(sizes.x, sizes.y, -sizes.z) / 2, color, duration);
        Debug.DrawLine(position + new Vector3(sizes.x, -sizes.y, -sizes.z) / 2, position + new Vector3(sizes.x, -sizes.y, sizes.z) / 2, color, duration);
        Debug.DrawLine(position + new Vector3(-sizes.x, -sizes.y, sizes.z) / 2, position + new Vector3(sizes.x, -sizes.y, sizes.z) / 2, color, duration);
        Debug.DrawLine(position + new Vector3(-sizes.x, -sizes.y, sizes.z) / 2, position + new Vector3(-sizes.x, sizes.y, sizes.z) / 2, color, duration);
    }

    public static void DrawGrid(Vector3 cellSize, Vector3 size, Vector3 origin, Color color, float duration)
    {
        for (int x = 0; x < size.x / cellSize.x; x++)
        {
            for (int y = 0; y < size.x / cellSize.x; y++)
            {
                for (int z = 0; z < size.x / cellSize.x; z++)
                {
                    DrawCube(origin + new Vector3(x * cellSize.x, y * cellSize.y, z * cellSize.z) + cellSize / 2, cellSize, color, duration);
                }
            }
        }
    }
}
