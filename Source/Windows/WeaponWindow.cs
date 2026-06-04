using UnityEngine;
using Verse;
using CWF.Controllers;
using CWF.ViewDrawers;

namespace CWF;

public class WeaponWindow : Window {
    private readonly HeaderDrawer _headerDrawer;
    private readonly AsideDrawer _asideDrawer;
    private readonly MainDrawer _mainDrawer;
    private readonly ModificationSession _session;
    private readonly JobDispatcher _jobDispatcher;

    public override Vector2 InitialSize => new(800f, 550f);

    public WeaponWindow(Thing weapon) {
        // === UI configs ===
        doCloseX = true;
        closeOnClickedOutside = false;
        draggable = true;
        resizeable = true;
        absorbInputAroundWindow = true;
        forcePause = true;

        // Controllers
        _session = new ModificationSession(weapon);
        var specDatabase = new SpecDatabase(_session);
        var interactionController = new InteractionController(weapon, _session, specDatabase.Recalculate);
        _jobDispatcher = new JobDispatcher(weapon);

        // Drawers
        _headerDrawer = new HeaderDrawer(weapon, _session, interactionController);
        _asideDrawer = new AsideDrawer(specDatabase);
        _mainDrawer = new MainDrawer(_session, interactionController.HandleSlotClick);
    }

    public override void DoWindowContents(Rect inRect) {
        const float headerHeight = 32f;
        const float headerGap = 12f;
        const float asideGap = 24f;
        const float asideWidthFraction = 0.24f;
        const float asideMaxWidth = 250f;

        // Rects
        var headerRect = new Rect(inRect.x, inRect.y, inRect.width, headerHeight);
        var bodyRect = new Rect(inRect.x, headerRect.yMax + headerGap, inRect.width,
            inRect.height - headerHeight - headerGap);

        // Body Aside
        var asideWidth = Mathf.Min(bodyRect.width * asideWidthFraction, asideMaxWidth);
        var asideRect = new Rect(bodyRect.x, bodyRect.y, asideWidth, bodyRect.height);

        // Body Main
        var mainRect = new Rect(asideRect.xMax + asideGap, bodyRect.y,
            bodyRect.width - asideWidth - asideGap, bodyRect.height);

        // Draw
        _headerDrawer.Draw(in headerRect);
        _asideDrawer.Draw(in asideRect);
        _mainDrawer.Draw(in mainRect);
    }

    public override void PostClose() {
        base.PostClose();
        _jobDispatcher.Dispatch(_session.CalculateNetChanges());
        _session.DisposePreview();
    }
}