using System.Text;
using UnityEngine;
using Verse.Sound;
using CWF.Extensions;

namespace CWF;

public class ModuleBrowserWindow : Window {
    private readonly List<FilterRow> _filters = [];
    private readonly Dictionary<ThingDef, RecipeDef> _recipeCache = new();
    private FilterRow _selectedFilter;
    private Vector2 _leftColumnScrollPosition = Vector2.zero;
    private Vector2 _rightColumnScrollPosition = Vector2.zero;

    private const float FilterRowHeight = 30f;
    private const float FilterRowPadding = 8f;
    private const float TitleContentGap = 6f;

    private sealed record FilterRow(string Label, List<ThingDef> Modules) {
        internal string CountLabel { get; } = Modules.Count.ToString();
    }

    public override Vector2 InitialSize => new(550f, 420f);

    public ModuleBrowserWindow(Thing weapon) {
        doCloseX = true;
        closeOnClickedOutside = false;
        draggable = true;
        resizeable = true;
        absorbInputAroundWindow = true;
        forcePause = true;

        var compatibleModules = ModuleDatabase.AllModuleDefs
            .Where(moduleDef => moduleDef.IsCompatibleWith(weapon.def))
            .OrderBy(moduleDef => moduleDef.LabelCap.ToString(), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // Sorting the flat list first means every GroupBy bucket inherits the same ordering.
        _filters.Add(new FilterRow("CWF_All".Translate(), compatibleModules));
        _filters.AddRange(compatibleModules
            .GroupBy(moduleDef => moduleDef.TraitModule().part)
            .OrderBy(group => group.Key.LabelCap.ToString(), StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new FilterRow(group.Key.LabelCap.ToString(), [.. group])));

        _selectedFilter = _filters[0];

        var relevantModules = compatibleModules.ToHashSet();
        foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading) {
            if (recipe.products.Count == 0) continue;

            var productDef = recipe.products[0].thingDef;
            if (relevantModules.Contains(productDef)) {
                _recipeCache.TryAdd(productDef, recipe);
            }
        }
    }

    public override void DoWindowContents(Rect inRect) {
        const float leftColumnWidth = 128f;
        const float columnGap = 16f;

        var leftRect = new Rect(inRect.x, inRect.y, leftColumnWidth, inRect.height);
        var rightRect = new Rect(leftRect.xMax + columnGap, inRect.y,
            inRect.width - leftColumnWidth - columnGap, inRect.height);

        DrawLeftColumn(leftRect);
        DrawRightColumn(rightRect);
    }

    private void DrawLeftColumn(in Rect rect) {
        var listing = new Listing_Standard();

        listing.Begin(rect);

        using (UIKit.Style(GameFont.Medium)) {
            listing.Label("CWF_Parts".Translate());
        }

        listing.Gap(TitleContentGap);

        if (_filters[0].Modules.Count == 0) {
            listing.End();
            return;
        }

        var viewHeight = rect.height - listing.CurHeight;
        var viewRect = new Rect(0f, listing.CurHeight, rect.width, viewHeight);
        var contentHeight = Mathf.Max(viewRect.height, _filters.Count * FilterRowHeight);
        var contentRect = new Rect(0f, 0f, viewRect.width, contentHeight);

        Widgets.BeginScrollView(viewRect, ref _leftColumnScrollPosition, contentRect, showScrollbars: false);

        var currentY = 0f;
        foreach (var filter in _filters) {
            var rowRect = new Rect(0f, currentY, contentRect.width, FilterRowHeight);
            if (DrawFilterRow(rowRect, filter, ReferenceEquals(_selectedFilter, filter))) {
                _selectedFilter = filter;
            }

            currentY += FilterRowHeight;
        }

        Widgets.EndScrollView();
        listing.End();
    }

    private void DrawRightColumn(in Rect rect) {
        var listing = new Listing_Standard();
        listing.Begin(rect);

        const float padding = 8f;
        const float titleHeight = 32f;
        var titleRect = listing.GetRect(titleHeight);
        var paddedTitleRect = new Rect(titleRect.x + padding, titleRect.y, titleRect.width - padding, titleRect.height);

        using (UIKit.Style(GameFont.Medium)) {
            Widgets.Label(paddedTitleRect, "CWF_CompatibleModules".Translate());
        }

        listing.Gap(TitleContentGap);

        var modulesToShow = _selectedFilter.Modules;

        if (modulesToShow.Count == 0) {
            var noModuleLabelRect = listing.GetRect(Text.LineHeight);
            noModuleLabelRect.x += padding;
            Widgets.Label(noModuleLabelRect, "CWF_NoCompatibleModules".Translate());
            listing.End();
            return;
        }

        const float rowHeight = 32f;

        var viewHeight = rect.height - listing.CurHeight;
        var viewRect = new Rect(0, listing.CurHeight, rect.width, viewHeight);

        var contentHeight = modulesToShow.Count * rowHeight;
        var contentRect = new Rect(0, 0, viewRect.width - 16f, contentHeight);

        Widgets.BeginScrollView(viewRect, ref _rightColumnScrollPosition, contentRect);

        var currentY = 0f;

        foreach (var moduleDef in modulesToShow) {
            var rowRect = new Rect(contentRect.x, currentY, contentRect.width, rowHeight);
            Widgets.DrawHighlightIfMouseover(rowRect);

            const float buttonSize = 24f;
            var buttonY = rowRect.y + (rowHeight - buttonSize) / 2f;

            var infoButtonRect = new Rect(rowRect.xMax - buttonSize, buttonY, buttonSize, buttonSize);
            var actionButtonRect = new Rect(infoButtonRect.x - buttonSize, buttonY, buttonSize, buttonSize);
            var labelRect = new Rect(rowRect.x + padding, rowRect.y,
                actionButtonRect.x - rowRect.x - padding, rowRect.height);

            if (Event.current.type == EventType.Repaint && Mouse.IsOver(labelRect)) {
                var sb = new StringBuilder();
                var traitDef = moduleDef.TraitModule().weaponTraitDef;
                sb.AppendLine(moduleDef.description);
                sb.AppendInNewLine(traitDef.GetTraitEffect());

                TooltipHandler.TipRegion(labelRect, sb.ToString());
            }

            using (UIKit.Style(anchor: TextAnchor.MiddleLeft)) {
                Widgets.Label(labelRect, moduleDef.LabelCap);
            }

            currentY += rowHeight;

            if (!Mouse.IsOver(rowRect)) continue;

            // craft
            if (_recipeCache.TryGetValue(moduleDef, out var recipe)) {
                if (recipe.AvailableNow) {
                    if (Widgets.ButtonImage(actionButtonRect, TexButton.Add, tooltip: "CWF_Craft".Translate())) {
                        AddCraftingBill(moduleDef, recipe);
                    }
                } else {
                    Widgets.ButtonImage(actionButtonRect, TexButton.Add, Color.gray,
                        tooltip: "CWF_CannotCraft".Translate());
                }
            }

            // info
            if (Widgets.ButtonImage(infoButtonRect.ContractedBy(2f),
                    TexButton.Info, tooltip: "DefInfoTip".Translate())) {
                Find.WindowStack.Add(new Dialog_InfoCard(moduleDef));
            }
        }

        Widgets.EndScrollView();
        listing.End();
    }

    private static void AddCraftingBill(ThingDef moduleDef, RecipeDef recipe) {
        var bench = Find.CurrentMap.listerBuildings.allBuildingsColonist
            .FirstOrDefault(b => b is IBillGiver && b.def.AllRecipes?.Contains(recipe) == true);

        if (bench == null) {
            Messages.Message("CWF_NoWorkbenchToCraftModule".Translate(moduleDef.Named("MODULE")),
                MessageTypeDefOf.RejectInput, false);
            return;
        }

        var bill = (Bill_Production)recipe.MakeNewBill();
        bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
        bill.repeatCount = 1;
        ((IBillGiver)bench).BillStack.AddBill(bill);

        SoundDefOf.Click.PlayOneShotOnCamera();
        Messages.Message("CWF_BillAdded".Translate(moduleDef.Named("MODULE"), bench.Named("BENCH")),
            new LookTargets(bench), MessageTypeDefOf.PositiveEvent);
    }

    private static bool DrawFilterRow(in Rect rect, FilterRow filter, bool selected) {
        var backgroundColor = selected
            ? new Color(1f, 1f, 1f, 0.14f)
            : new Color(1f, 1f, 1f, 0.04f);

        Widgets.DrawBoxSolid(rect, backgroundColor);
        Widgets.DrawHighlightIfMouseover(rect);

        var labelRect = new Rect(rect.x + FilterRowPadding, rect.y, rect.width - 44f, rect.height);
        var countRect = new Rect(rect.xMax - 32f, rect.y, 24f, rect.height);
        var labelColor = selected ? Color.white : new Color(0.85f, 0.85f, 0.85f);
        var countColor = selected ? Color.white : Color.gray;

        using (UIKit.Style(color: labelColor, anchor: TextAnchor.MiddleLeft)) {
            Widgets.Label(labelRect, filter.Label);
        }

        using (UIKit.Style(GameFont.Tiny, countColor, TextAnchor.MiddleRight)) {
            Widgets.Label(countRect, filter.CountLabel);
        }

        return Widgets.ButtonInvisible(rect);
    }
}