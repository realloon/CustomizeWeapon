using System.Text;
using UnityEngine;
using CWF.Extensions;

namespace CWF.ViewDrawers;

public class MainDrawer(ModificationSession session, Action<PartDef, WeaponTraitDef?> onSlotClick) {
    private const float SlotSize = 56f;
    private const float SlotPadding = 12f;

    public void Draw(Rect rect) {
        // define gird row Height
        const float topRowHeight = SlotSize + SlotPadding;
        const float bottomRowHeight = SlotSize + SlotPadding;
        var middleRowHeight = rect.height - topRowHeight - bottomRowHeight;

        // define gird col with
        const float leftColWidth = SlotSize + SlotPadding;
        const float rightColWidth = SlotSize + SlotPadding;
        var middleColWidth = rect.width - leftColWidth - rightColWidth;

        if (middleRowHeight <= 0 || middleColWidth <= 0) return;

        // define gird cell
        var topCenterRect = new Rect(rect.x + leftColWidth, rect.y, middleColWidth, topRowHeight);
        var middleLeftRect = new Rect(rect.x, rect.y + topRowHeight, leftColWidth, middleRowHeight);
        var middleCenterRect =
            new Rect(rect.x + leftColWidth, rect.y + topRowHeight, middleColWidth, middleRowHeight);
        var middleRightRect = new Rect(rect.x + leftColWidth + middleColWidth, rect.y + topRowHeight, rightColWidth,
            middleRowHeight);
        var bottomCenterRect = new Rect(rect.x + leftColWidth, rect.y + topRowHeight + middleRowHeight,
            middleColWidth, bottomRowHeight);

        WeaponPreviewDrawer.Draw(middleCenterRect, session.PreviewWeapon);

        DrawPartGroup(topCenterRect, PartGroup.Top, horizontal: true);
        DrawPartGroup(bottomCenterRect, PartGroup.Bottom, horizontal: true);
        DrawPartGroup(middleLeftRect, PartGroup.Left, horizontal: false);
        DrawPartGroup(middleRightRect, PartGroup.Right, horizontal: false);
    }

    // === Helper ===
    private void DrawPartGroup(Rect container, PartGroup group, bool horizontal) {
        var groupParts = session.AvailableParts
            .Where(p => p.group == group)
            .OrderBy(p => p.order)
            .ToList();
        var count = groupParts.Count;
        if (count == 0) return;

        const float step = SlotSize + SlotPadding;
        var span = count * SlotSize + (count - 1) * SlotPadding;
        var startX = container.center.x - (horizontal ? span : SlotSize) / 2f;
        var startY = container.center.y - (horizontal ? SlotSize : span) / 2f;

        for (var i = 0; i < count; i++) {
            var offset = i * step;
            var slotRect = new Rect(
                startX + (horizontal ? offset : 0f),
                startY + (horizontal ? 0f : offset),
                SlotSize, SlotSize);
            TryDrawSlot(groupParts[i], slotRect);
        }
    }

    private void TryDrawSlot(PartDef part, Rect rect) {
        var installedTrait = session.GetInstalledTraitFor(part);
        var isHovered = Mouse.IsOver(rect);
        bool clicked;

        if (installedTrait != null) {
            Widgets.DrawOptionBackground(rect, isHovered);

            var moduleGraphicData = GetSlotGraphicData(part, installedTrait, out var isFallback);
            if (moduleGraphicData != null) {
                DrawModuleTexture(rect, moduleGraphicData, !isFallback);
            } else {
                using (UIKit.Style(GameFont.Tiny, anchor: TextAnchor.MiddleCenter)) {
                    Widgets.Label(rect, installedTrait.LabelCap);
                }
            }

            if (Event.current.type == EventType.Repaint && isHovered) {
                var tipSb = new StringBuilder();
                tipSb.AppendLine($"<b>{installedTrait.LabelCap}</b>");
                tipSb.AppendLine(installedTrait.description);
                tipSb.AppendInNewLine(installedTrait.GetTraitEffect());

                TooltipHandler.TipRegion(rect, tipSb.ToString());
            }

            clicked = Widgets.ButtonInvisible(rect);
        } else {
            clicked = DrawPartSlot(rect, part.LabelCap, isHovered);
        }

        if (clicked) onSlotClick.Invoke(part, installedTrait);
    }

    private ModuleGraphicData? GetSlotGraphicData(PartDef part, WeaponTraitDef installedTrait, out bool isFallback) {
        isFallback = false;

        var moduleGraphicData = session.PreviewWeapon.TryGetComp<CompDynamicGraphic>()
            ?.GetGraphicDataFor(installedTrait);

        if (moduleGraphicData != null && !moduleGraphicData.texturePath.NullOrEmpty()) {
            return moduleGraphicData;
        }

        if (part.fallbackTexturePath.NullOrEmpty()) {
            return null;
        }

        isFallback = true;
        return new ModuleGraphicData {
            texturePath = part.fallbackTexturePath
        };
    }

    private static void DrawModuleTexture(Rect rect, ModuleGraphicData moduleGraphicData, bool drawOutline) {
        if (moduleGraphicData.texturePath.NullOrEmpty()) return;

        var mainTexture = ContentFinder<Texture2D>.Get(moduleGraphicData.texturePath);
        if (mainTexture == null) return;

        if (drawOutline) {
            var outlineTexture = CompDynamicGraphic.GetModuleOutline(mainTexture);
            if (outlineTexture != null) {
                Widgets.DrawTextureFitted(rect, outlineTexture, 1f);
            }
        }

        Widgets.DrawTextureFitted(rect, mainTexture, 1f);
    }

    /// Returns a boolean indicating whether the element was clicked.
    private static bool DrawPartSlot(Rect rect, string label, bool isHovered) {
        Widgets.DrawOptionBackground(rect, isHovered);

        // render label
        var labelRect = new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, rect.height - 3f);
        using (UIKit.Style(GameFont.Tiny, Color.gray, TextAnchor.LowerLeft)) {
            Widgets.Label(labelRect, label);
        }

        // render '+'
        using (UIKit.Style(GameFont.Medium, anchor: TextAnchor.MiddleCenter)) {
            Widgets.Label(rect, "+");
        }

        return Widgets.ButtonInvisible(rect);
    }
}