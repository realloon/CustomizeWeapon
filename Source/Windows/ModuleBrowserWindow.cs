using System.Text;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.Sound;
using CWF.Extensions;

namespace CWF;

public class ModuleBrowserWindow : Window {
    private readonly Dictionary<PartDef, List<ThingDef>> _groupedModules = new();
    private readonly Dictionary<ThingDef, RecipeDef> _recipeCache = new();
    private PartDef? _selectedPart;
    private Vector2 _leftColumnScrollPosition = Vector2.zero;
    private Vector2 _rightColumnScrollPosition = Vector2.zero;

    private const float FilterRowHeight = 30f;
    private const float FilterRowPadding = 8f;
    private const float TitleContentGap = 6f;

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
            .ToArray();

        foreach (var moduleDef in compatibleModules) {
            var part = moduleDef.GetModExtension<TraitModuleExtension>().part;

            if (!_groupedModules.ContainsKey(part)) {
                _groupedModules[part] = [];
            }

            _groupedModules[part].Add(moduleDef);
        }

        var relevantModules = compatibleModules.ToHashSet();
        foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading) {
            if (recipe.products.Empty()) continue;

            var productDef = recipe.products[0].thingDef;
            if (relevantModules.Contains(productDef)) {
                _recipeCache.TryAdd(productDef, recipe);
            }
        }

        _groupedModules = _groupedModules
            .OrderBy(kvp => kvp.Key.LabelCap.ToString())
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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

        UIKit.WithStyle(() => listing.Label("CWF_Parts".Translate()), GameFont.Medium);
        listing.Gap(TitleContentGap);

        if (_groupedModules.NullOrEmpty()) {
            listing.End();
            return;
        }

        var filters = new List<(PartDef? Part, string Label, int Count)> {
            (null, "CWF_All".Translate(), _groupedModules.Values.Sum(modules => modules.Count))
        };

        filters.AddRange(
            _groupedModules.Select(kvp => ((PartDef?)kvp.Key, kvp.Key.LabelCap.ToString(), kvp.Value.Count)));

        var viewHeight = rect.height - listing.CurHeight;
        var viewRect = new Rect(0f, listing.CurHeight, rect.width, viewHeight);
        var contentHeight = Mathf.Max(viewRect.height, filters.Count * FilterRowHeight);
        var contentRect = new Rect(0f, 0f, viewRect.width, contentHeight);

        Widgets.BeginScrollView(viewRect, ref _leftColumnScrollPosition, contentRect, showScrollbars: false);

        var currentY = 0f;
        foreach (var filter in filters) {
            var rowRect = new Rect(0f, currentY, contentRect.width, FilterRowHeight);
            DrawFilterRow(rowRect, filter.Label, filter.Count, _selectedPart == filter.Part,
                () => { _selectedPart = filter.Part; });
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

        UIKit.WithStyle(() => Widgets.Label(paddedTitleRect, "CWF_CompatibleModules".Translate()), GameFont.Medium);
        listing.Gap(TitleContentGap);

        var modulesToShow = _selectedPart switch {
            null => _groupedModules.Values.SelectMany(list => list).ToList(),
            _ => _groupedModules.GetValueOrDefault(_selectedPart) ?? []
        };

        if (modulesToShow.Empty()) {
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

        foreach (var moduleDef in modulesToShow.OrderBy(m => m.LabelCap.ToString())) {
            var rowRect = new Rect(contentRect.x, currentY, contentRect.width, rowHeight);
            Widgets.DrawHighlightIfMouseover(rowRect);

            const float buttonSize = 24f;
            var buttonY = rowRect.y + (rowHeight - buttonSize) / 2f;

            var infoButtonRect = new Rect(rowRect.xMax - buttonSize, buttonY, buttonSize, buttonSize);
            var actionButtonRect = new Rect(infoButtonRect.x - buttonSize, buttonY, buttonSize, buttonSize);
            var labelRect = new Rect(rowRect.x + padding, rowRect.y,
                actionButtonRect.x - rowRect.x - padding, rowRect.height);

            var sb = new StringBuilder();
            var traitDef = moduleDef.GetModExtension<TraitModuleExtension>().weaponTraitDef;
            sb.AppendLine(moduleDef.description);
            sb.AppendInNewLine(traitDef.GetTraitEffect());

            TooltipHandler.TipRegion(labelRect, sb.ToString());

            UIKit.WithStyle(() => Widgets.Label(labelRect, moduleDef.LabelCap), anchor: TextAnchor.MiddleLeft);

            currentY += rowHeight;

            if (!Mouse.IsOver(rowRect)) continue;

            // craft
            if (_recipeCache.TryGetValue(moduleDef, out var recipe)) {
                if (recipe.AvailableNow) {
                    if (Widgets.ButtonImage(actionButtonRect, TexButton.Add, tooltip: "CWF_Craft".Translate())) {
                        TryAddCraftingBill(moduleDef);
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

    private void TryAddCraftingBill(ThingDef moduleDef) {
        if (!_recipeCache.TryGetValue(moduleDef, out var recipe)) return;

        if (recipe == null) {
            Log.Warning("Cannot craft this module.");
            return;
        }

        var bench = Find.CurrentMap.listerBuildings.allBuildingsColonist
            .OfType<IBillGiver>()
            .FirstOrDefault(b => b is Thing thing && (thing.def.AllRecipes?.Contains(recipe) ?? false));

        if (bench == null) {
            Messages.Message("CWF_NoWorkbenchToCraftModule".Translate(moduleDef.Named("MODULE")),
                MessageTypeDefOf.RejectInput, false);
            return;
        }

        var bill = recipe.MakeNewBill();
        if (bill is Bill_Production billProduction) {
            billProduction.repeatMode = BillRepeatModeDefOf.RepeatCount;
            billProduction.repeatCount = 1;
            bench.BillStack.AddBill(bill);
        }

        SoundDefOf.Click.PlayOneShotOnCamera();
        Messages.Message("CWF_BillAdded".Translate(moduleDef.Named("MODULE"), bench.Named("BENCH")),
            new LookTargets((Thing)bench), MessageTypeDefOf.PositiveEvent);
    }

    private static void DrawFilterRow(in Rect rect, string label, int count, bool selected, Action onClick) {
        var backgroundColor = selected
            ? new Color(1f, 1f, 1f, 0.14f)
            : new Color(1f, 1f, 1f, 0.04f);

        Widgets.DrawBoxSolid(rect, backgroundColor);
        Widgets.DrawHighlightIfMouseover(rect);

        if (selected) {
            var accentRect = new Rect(rect.x, rect.y + 4f, 3f, rect.height - 8f);
            Widgets.DrawBoxSolid(accentRect, new Color(0.35f, 0.8f, 1f));
        }

        var labelRect = new Rect(rect.x + FilterRowPadding, rect.y, rect.width - 44f, rect.height);
        var countRect = new Rect(rect.xMax - 32f, rect.y, 24f, rect.height);
        var labelColor = selected ? Color.white : new Color(0.85f, 0.85f, 0.85f);
        var countColor = selected ? Color.white : Color.gray;

        UIKit.WithStyle(() => Widgets.Label(labelRect, label), color: labelColor, anchor: TextAnchor.MiddleLeft);
        UIKit.WithStyle(() => Widgets.Label(countRect, count.ToString()), GameFont.Tiny, countColor,
            TextAnchor.MiddleRight);

        if (Widgets.ButtonInvisible(rect)) {
            onClick();
        }
    }
}