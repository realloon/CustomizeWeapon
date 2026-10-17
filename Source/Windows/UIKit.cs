using UnityEngine;

namespace CWF;

internal static class UIKit {
    private static readonly Dictionary<Color, Texture2D> ColorSwatches = [];

    internal static Texture2D GetColorSwatch(Color color) {
        if (ColorSwatches.TryGetValue(color, out var cached)) {
            return cached;
        }

        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        var pixels = new Color32[16 * 16];
        var color32 = (Color32)color;
        for (var i = 0; i < pixels.Length; i++) {
            pixels[i] = color32;
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        ColorSwatches[color] = texture;
        return texture;
    }

    internal static void WithStyle(Action drawAction, GameFont font = GameFont.Small, Color? color = null,
        TextAnchor anchor = TextAnchor.UpperLeft) {
        var originalFont = Text.Font;
        var originalColor = GUI.color;
        var originalAnchor = Text.Anchor;

        Text.Font = font;
        GUI.color = color ?? Color.white;
        Text.Anchor = anchor;

        drawAction();

        Text.Font = originalFont;
        GUI.color = originalColor;
        Text.Anchor = originalAnchor;
    }
}