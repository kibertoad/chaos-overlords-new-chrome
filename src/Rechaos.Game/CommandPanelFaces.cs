using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>What a command panel shows over its baked confirm face.</summary>
public enum CommandPanelFaceState
{
    /// <summary>Nothing is drawn, so the face baked into the panel art shows.</summary>
    NotDrawn,
    Enabled,
    Disabled
}

public enum CommandPanelButton
{
    Cancel,
    Confirm
}

/// <summary>
/// The Cancel and confirm faces of the Equip, Give, Sell and Move panels (SCR-EQUIP-001,
/// SCR-GIVE-001, SCR-SELL-001, SCR-MOVE-001). Both act on a release inside the half-open
/// 49-by-22 targets; the images are 50 by 23, one pixel larger.
/// </summary>
public static class CommandPanelFaces
{
    public static Rectangle CancelHit => SharedPanelLayout.CommandCancel;
    public static Rectangle ConfirmHit => SharedPanelLayout.CommandOk;

    public static Rectangle Face(CommandPanelButton button) => button switch
    {
        CommandPanelButton.Cancel => new Rectangle(CancelHit.X, CancelHit.Y, 50, 23),
        CommandPanelButton.Confirm => new Rectangle(ConfirmHit.X, ConfirmHit.Y, 50, 23),
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };

    public static Rectangle Hit(CommandPanelButton button) => button switch
    {
        CommandPanelButton.Cancel => CancelHit,
        CommandPanelButton.Confirm => ConfirmHit,
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };

    /// <summary>
    /// PX00129 image <c>fn_00418E66</c> copies over the confirm face (FND-UI-019): the enabled
    /// image at (50,386), the disabled one at (100,386), or nothing.
    /// </summary>
    public static Rectangle? Source(CommandPanelFaceState state) => state switch
    {
        CommandPanelFaceState.NotDrawn => null,
        CommandPanelFaceState.Enabled => new Rectangle(50, 386, 50, 23),
        CommandPanelFaceState.Disabled => new Rectangle(100, 386, 50, 23),
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    /// <summary>
    /// Image the shared held-button helper <c>fn_00418821</c> shows while the button is held
    /// inside its target (FND-COMLINK-003).
    /// </summary>
    public static Rectangle HeldSource(CommandPanelButton button) => button switch
    {
        CommandPanelButton.Cancel => new Rectangle(50, 409, 50, 23),
        CommandPanelButton.Confirm => new Rectangle(50, 386, 50, 23),
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };

    public static CommandPanelButton? ButtonAt(Point point) =>
        CancelHit.Contains(point) ? CommandPanelButton.Cancel
        : ConfirmHit.Contains(point) ? CommandPanelButton.Confirm
        : null;

    /// <summary>
    /// Face drawn when a panel opens: enabled when the gang already has the panel's order
    /// (FND-EQUIP-010, FND-GIVE-001, FND-SELL-001, FND-MOVE-004), otherwise the baked face.
    /// </summary>
    public static CommandPanelFaceState OnOpening(bool gangHasPanelOrder) =>
        gangHasPanelOrder ? CommandPanelFaceState.Enabled : CommandPanelFaceState.NotDrawn;

    /// <summary>
    /// Face after a change of selection: every handler redraws it with its confirm predicate,
    /// so it shows enabled or disabled from then on.
    /// </summary>
    public static CommandPanelFaceState AfterChange(bool canConfirm) =>
        canConfirm ? CommandPanelFaceState.Enabled : CommandPanelFaceState.Disabled;
}
