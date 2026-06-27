using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public enum WhiteBoardTool
{
    None = 0,
    Pen = 1,
    Eraser = 2,
}

public static class WhiteBoardDrawingSurface
{
    public const int Width = 1920;
    public const int Height = 1080;

    private static readonly Color32 s_backgroundColor = new(255, 255, 255, 0);
    private static Texture2D s_texture;
    private static Sprite s_sprite;

    public static event Action Changed;

    public static Texture2D Texture
    {
        get
        {
            EnsureTexture();
            return s_texture;
        }
    }

    public static Sprite Sprite
    {
        get
        {
            EnsureTexture();
            return s_sprite;
        }
    }

    public static void ApplyStroke(WhiteBoardTool tool, int thickness, Color32 color, IReadOnlyList<Vector2Int> points)
    {
        if (tool == WhiteBoardTool.None || points == null || points.Count == 0)
            return;

        EnsureTexture();

        Color32 drawColor = tool == WhiteBoardTool.Eraser ? s_backgroundColor : color;
        int radius = Mathf.Max(1, thickness) / 2;

        DrawCircle(points[0], radius, drawColor);
        for (int i = 1; i < points.Count; i++)
            DrawLine(points[i - 1], points[i], radius, drawColor);

        s_texture.Apply(false);
        Changed?.Invoke();
    }

    public static string CreatePayload(WhiteBoardTool tool, int thickness, Color32 color, IReadOnlyList<Vector2Int> points)
    {
        StringBuilder builder = new();
        builder.Append((int)tool);
        builder.Append('|');
        builder.Append(Mathf.Clamp(thickness, 1, 50));
        builder.Append('|');
        builder.Append(ColorUtility.ToHtmlStringRGBA(color));
        builder.Append('|');

        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0)
                builder.Append(';');

            Vector2Int point = ClampPoint(points[i]);
            builder.Append(point.x);
            builder.Append(',');
            builder.Append(point.y);
        }

        return builder.ToString();
    }

    public static bool TryApplyPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        string[] parts = payload.Split('|');
        if (parts.Length != 4)
            return false;

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int toolValue))
            return false;

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int thickness))
            return false;

        if (!ColorUtility.TryParseHtmlString($"#{parts[2]}", out Color color))
            return false;

        string[] encodedPoints = parts[3].Split(';');
        List<Vector2Int> points = new(encodedPoints.Length);
        for (int i = 0; i < encodedPoints.Length; i++)
        {
            string[] xy = encodedPoints[i].Split(',');
            if (xy.Length != 2)
                return false;

            if (!int.TryParse(xy[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x))
                return false;

            if (!int.TryParse(xy[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                return false;

            points.Add(ClampPoint(new Vector2Int(x, y)));
        }

        ApplyStroke((WhiteBoardTool)toolValue, thickness, color, points);
        return true;
    }

    public static Vector2Int ClampPoint(Vector2Int point)
    {
        return new Vector2Int(Mathf.Clamp(point.x, 0, Width - 1), Mathf.Clamp(point.y, 0, Height - 1));
    }

    private static void EnsureTexture()
    {
        if (s_texture != null)
            return;

        s_texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color32[] pixels = new Color32[Width * Height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = s_backgroundColor;
        s_texture.SetPixels32(pixels);
        s_texture.Apply(false);

        s_sprite = Sprite.Create(s_texture, new Rect(0f, 0f, Width, Height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void DrawLine(Vector2Int from, Vector2Int to, int radius, Color32 color)
    {
        int dx = Mathf.Abs(to.x - from.x);
        int dy = Mathf.Abs(to.y - from.y);
        int steps = Mathf.Max(dx, dy);
        if (steps == 0)
        {
            DrawCircle(to, radius, color);
            return;
        }

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t));
            DrawCircle(new Vector2Int(x, y), radius, color);
        }
    }

    private static void DrawCircle(Vector2Int center, int radius, Color32 color)
    {
        int safeRadius = Mathf.Max(1, radius);
        int sqrRadius = safeRadius * safeRadius;
        int minX = Mathf.Max(0, center.x - safeRadius);
        int maxX = Mathf.Min(Width - 1, center.x + safeRadius);
        int minY = Mathf.Max(0, center.y - safeRadius);
        int maxY = Mathf.Min(Height - 1, center.y + safeRadius);

        for (int y = minY; y <= maxY; y++)
        {
            int yy = y - center.y;
            for (int x = minX; x <= maxX; x++)
            {
                int xx = x - center.x;
                if (xx * xx + yy * yy <= sqrRadius)
                    s_texture.SetPixel(x, y, color);
            }
        }
    }
}
