using System.Text;
using UnityEngine;
using RimWorld;
using Verse;
using CWF.Extensions;

namespace CWF.ViewDrawers;

public class MainDrawer(WeaponModificationSession session, Action<PartDef, WeaponTraitDef?> onSlotClick) {
    // private readonly Thing _weapon = weapon;
    private const float SlotSize = 56f;
    private const float SlotPadding = 12f;

    private enum Direction {
        Horizontal,
        Vertical
    }

    public void Draw(in Rect rect) {
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

        WeaponPreviewDrawer.Draw(in middleCenterRect, session.PreviewWeapon);

        var availableParts = session.AvailableParts;

        var topParts = availableParts.Where(p => p.group == PartGroup.Top).OrderBy(p => p.order).ToList();
        var bottomParts = availableParts.Where(p => p.group == PartGroup.Bottom).OrderBy(p => p.order).ToList();
        var leftParts = availableParts.Where(p => p.group == PartGroup.Left).OrderBy(p => p.order).ToList();
        var rightParts = availableParts.Where(p => p.group == PartGroup.Right).OrderBy(p => p.order).ToList();

        DrawPartGroup(in topCenterRect, topParts, Direction.Horizontal);
        DrawPartGroup(in bottomCenterRect, bottomParts, Direction.Horizontal);
        DrawPartGroup(in middleLeftRect, leftParts, Direction.Vertical);
        DrawPartGroup(in middleRightRect, rightParts, Direction.Vertical);
    }

    // === Helper ===
    private void DrawPartGroup(in Rect container, List<PartDef> groupParts, Direction direction) {
        if (groupParts.Empty()) return;

        var count = groupParts.Count;

        if (direction == Direction.Horizontal) {
            var totalWidth = count * SlotSize + Math.Max(0, count - 1) * SlotPadding;
            var startX = container.center.x - totalWidth / 2f;
            var startY = container.center.y - SlotSize / 2f;

            for (var i = 0; i < count; i++) {
                var part = groupParts[i];
                var slotRect = new Rect(startX + i * (SlotSize + SlotPadding), startY, SlotSize, SlotSize);
                TryDrawSlot(part, in slotRect);
            }
        } else {
            // Vertical
            var totalHeight = count * SlotSize + Math.Max(0, count - 1) * SlotPadding;
            var startX = container.center.x - SlotSize / 2f;
            var startY = container.center.y - totalHeight / 2f;

            for (var i = 0; i < count; i++) {
                var part = groupParts[i];
                var slotRect = new Rect(startX, startY + i * (SlotSize + SlotPadding), SlotSize, SlotSize);
                TryDrawSlot(part, in slotRect);
            }
        }
    }

    private void TryDrawSlot(PartDef part, in Rect rect) {
        if (!session.AvailableParts.Contains(part)) return;

        var installedTrait = session.GetInstalledTraitFor(part);
        bool clicked;

        if (installedTrait != null) {
            Widgets.DrawOptionBackground(rect, Mouse.IsOver(rect));

            var moduleGraphicData = session.PreviewWeapon.TryGetComp<CompDynamicGraphic>()
                ?.GetGraphicDataFor(installedTrait);

            if (moduleGraphicData != null && !moduleGraphicData.texturePath.IsNullOrEmpty()) {
                DrawModuleTexture(in rect, moduleGraphicData);
            } else {
                var inRect = rect;
                UIKit.WithStyle(() => Widgets.Label(inRect, installedTrait.LabelCap),
                    GameFont.Tiny, anchor: TextAnchor.MiddleCenter);
            }

            var tipSb = new StringBuilder();
            tipSb.AppendLine($"<b>{installedTrait.LabelCap}</b>");
            tipSb.AppendLine(installedTrait.description);
            tipSb.AppendInNewLine(installedTrait.GetTraitEffect());

            TooltipHandler.TipRegion(rect, tipSb.ToString());

            clicked = Widgets.ButtonInvisible(rect);
        } else {
            clicked = DrawPartSlot(in rect, part.LabelCap);
        }

        if (clicked) onSlotClick.Invoke(part, installedTrait);
    }

    private static void DrawModuleTexture(in Rect rect, ModuleGraphicData moduleGraphicData) {
        // Outline
        var outlineTexture = CompDynamicGraphic.GetOutlineTexture(moduleGraphicData);
        if (outlineTexture != null) {
            Widgets.DrawTextureFitted(rect, outlineTexture, 1f);
        }

        // module
        var mainTexture = ContentFinder<Texture2D>.Get(moduleGraphicData.texturePath);
        if (mainTexture != null) {
            Widgets.DrawTextureFitted(rect, mainTexture, 1f);
        }
    }

    /// Returns a boolean indicating whether the element was clicked.
    private static bool DrawPartSlot(in Rect rect, string label) {
        Widgets.DrawOptionBackground(rect, Mouse.IsOver(rect));

        // render label
        var labelRect = new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, rect.height - 3f);
        UIKit.WithStyle(() => Widgets.Label(labelRect, label), GameFont.Tiny, Color.gray, TextAnchor.LowerLeft);

        // render '+'
        var inRect = rect;
        UIKit.WithStyle(() => Widgets.Label(inRect, "+"), GameFont.Medium, anchor: TextAnchor.MiddleCenter);

        return Widgets.ButtonInvisible(rect);
    }
}