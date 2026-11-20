using UnityEngine;

namespace CWF;

// ReSharper disable once InconsistentNaming
public class Dialog_TextInput : Window {
    private string _currentValue;
    private readonly Action<string> _onConfirm;
    private readonly string _title;
    private readonly string _confirmButtonText;
    private readonly string _cancelButtonText;
    private bool _focusedField;
    private bool _shouldExecuteOnClose = true;

    public Dialog_TextInput(
        string initialValue,
        Action<string> onConfirm,
        string? title = null,
        string? confirmButtonText = null,
        string? cancelButtonText = null) {
        _currentValue = initialValue;
        _onConfirm = onConfirm;
        _title = title ?? "CWF_InputTitle".Translate();
        _confirmButtonText = confirmButtonText ?? "Confirm".Translate();
        _cancelButtonText = cancelButtonText ?? "Cancel".Translate();

        forcePause = true;
        closeOnClickedOutside = false; // modal dialog
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize => new(350f, 200f);

    public override void DoWindowContents(Rect inRect) {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        // Render title
        using (UIKit.Style(GameFont.Medium)) {
            listing.Label(_title);
        }

        listing.Gap();

        // Render input
        GUI.SetNextControlName("TextInputField");
        _currentValue = listing.TextEntry(_currentValue); // no check internally
        if (!_focusedField) {
            UI.FocusControl("TextInputField", this);
            _focusedField = true;
        }

        listing.Gap();

        // Footer
        const float buttonWidth = 100f;
        var buttonY = inRect.height - 35f;

        // Cancel
        var cancelButtonRect = new Rect(inRect.width - buttonWidth * 2 - 10f, buttonY, buttonWidth, 35f);
        if (Widgets.ButtonText(cancelButtonRect, _cancelButtonText)) {
            _shouldExecuteOnClose = false;
            Close();
        }

        // Confirm
        var confirmButtonRect = new Rect(inRect.width - buttonWidth, buttonY, buttonWidth, 35f);
        if (Widgets.ButtonText(confirmButtonRect, _confirmButtonText)) {
            Close();
        }

        listing.End();
    }

    public override void OnCancelKeyPressed() {
        _shouldExecuteOnClose = false;
        base.OnCancelKeyPressed();
    }

    public override void PreClose() {
        base.PreClose();
        if (!_shouldExecuteOnClose) return;

        _onConfirm.Invoke(_currentValue);
    }
}