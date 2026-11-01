using UnityEngine;

public static class ColorsUtil
{
    // Convertit une couleur de l'enum en couleur Unity
    public static Color ToColor(Colors c)
    {
        if (c == Colors.None) return new Color(0.3f, 0.3f, 0.3f);

        float r = (c & Colors.Red) != 0 ? 1f : 0f;
        float g = (c & Colors.Green) != 0 ? 1f : 0f;
        float b = (c & Colors.Blue) != 0 ? 1f : 0f;
        return new Color(r, g, b);
    }
}