using UnityEngine;

namespace CWF.ViewDrawers;

internal static class WeaponPreviewDrawer {
    internal static void Draw(Rect rect, Thing weapon) {
        var previewTexture = weapon.UIIconOverride;
        if (previewTexture == null) {
            Widgets.ThingIcon(rect, weapon);
            return;
        }

        var textureAspect = (float)previewTexture.width / previewTexture.height;
        var boundsAspect = rect.width / rect.height;

        Rect drawRect;

        if (textureAspect >= boundsAspect) {
            var drawHeight = rect.width / textureAspect;
            drawRect = new Rect(rect.x, rect.center.y - drawHeight / 2f, rect.width, drawHeight);
        } else {
            var drawWidth = rect.height * textureAspect;
            drawRect = new Rect(rect.center.x - drawWidth / 2f, rect.y, drawWidth, rect.height);
        }

        Widgets.DrawTextureFitted(drawRect, previewTexture, 1f);
    }
}