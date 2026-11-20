using UnityEngine;

namespace CWF;

// ReSharper disable once InconsistentNaming
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

    internal static StyleScope Style(GameFont font = GameFont.Small, Color? color = null,
        TextAnchor anchor = TextAnchor.UpperLeft) => new(font, color ?? Color.white, anchor);

    internal readonly ref struct StyleScope {
        private readonly GameFont _previousFont;
        private readonly Color _previousColor;
        private readonly TextAnchor _previousAnchor;

        internal StyleScope(GameFont font, Color color, TextAnchor anchor) {
            _previousFont = Text.Font;
            _previousColor = GUI.color;
            _previousAnchor = Text.Anchor;

            Text.Font = font;
            GUI.color = color;
            Text.Anchor = anchor;
        }

        internal void Dispose() {
            Text.Font = _previousFont;
            GUI.color = _previousColor;
            Text.Anchor = _previousAnchor;
        }
    }
}