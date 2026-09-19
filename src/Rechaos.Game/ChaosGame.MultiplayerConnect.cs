using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// The controls on the online screen's connect form: which server, which role, and the settings a
/// host fixes before the lobby exists to carry them.
/// </summary>
public sealed partial class ChaosGame
{
    /// <summary>Whether the connect screen is offering a password for what it is about to do.</summary>
    private bool OnlinePasswordApplies => OnlineConnectPolicy.PasswordApplies(
        _online.Role == OnlineConnectRole.Host, _online.PublicListing);

    /// <summary>
    /// The fields the form is currently asking for, in the order they are read.
    /// </summary>
    /// <remarks>
    /// Top to bottom, which is also the order TAB moves the caret in and the order
    /// <see cref="RefocusOnlineForm"/> starts from: a tab order that disagrees with the screen sends
    /// the caret somewhere the eye is not. The server comes last because it is last on the form, and
    /// because the name is what a player arriving here actually has to fill in.
    /// </remarks>
    private TextField[] OnlineFields
    {
        get
        {
            var fields = new List<TextField>(4) { _online.DisplayName };
            if (_online.Role == OnlineConnectRole.Join) fields.Add(_online.JoinCode);
            if (OnlinePasswordApplies) fields.Add(_online.Password);
            if (_online.Service == OnlineServiceMode.Custom) fields.Add(_online.Server);
            return [.. fields];
        }
    }

    private void FocusNextOnlineField()
    {
        var fields = OnlineFields;
        var current = Array.FindIndex(fields, field => field.IsFocused);
        foreach (var field in fields) field.IsFocused = false;
        fields[Mod(current + 1, fields.Length)].IsFocused = true;
    }

    /// <summary>
    /// Moves focus to the field that was clicked, if one was.
    /// </summary>
    /// <remarks>
    /// A click that misses every field, on a button or on the panel, leaves focus alone. Clearing it
    /// would mean a player who pressed HOST and then carried on typing had their keystrokes go
    /// nowhere, with a caret still blinking somewhere to say they had not.
    /// </remarks>
    private void FocusOnlineField(Point point)
    {
        var hits = new List<(Rectangle Bounds, TextField Field)>(4)
        {
            (OnlineConnectLayout.Name, _online.DisplayName)
        };
        if (_online.Role == OnlineConnectRole.Join)
            hits.Add((OnlineConnectLayout.JoinCode, _online.JoinCode));
        if (OnlinePasswordApplies) hits.Add((OnlineConnectLayout.Password, _online.Password));
        if (_online.Service == OnlineServiceMode.Custom)
            hits.Add((OnlineConnectLayout.Server, _online.Server));
        if (!hits.Any(hit => hit.Bounds.Contains(point))) return;
        foreach (var (bounds, field) in hits) field.IsFocused = bounds.Contains(point);
    }

    private void SelectOnlineService(OnlineServiceMode service)
    {
        if (_online.Stage != MultiplayerStage.Connect || _online.Service == service) return;
        _online.Service = service;
        RefocusOnlineForm();
        SavePreferences();
        BeginServerProbe();
    }

    private void SelectOnlineRole(OnlineConnectRole role)
    {
        if (_online.Stage != MultiplayerStage.Connect || _online.Role == role) return;
        _online.Role = role;
        RefocusOnlineForm();
    }

    /// <summary>
    /// Chooses between a session anyone can find under BROWSE and one only its join code reaches.
    /// </summary>
    /// <remarks>
    /// Going private forgets the password rather than keeping it out of sight. The field is gone
    /// from the screen at that point, so a secret left behind it would be demanded of everyone the
    /// host hands the code to, by a host who can no longer read what it is.
    /// </remarks>
    private void SelectOnlineListing(bool publicly)
    {
        if (_online.Stage != MultiplayerStage.Connect || _online.PublicListing == publicly) return;
        _online.PublicListing = publicly;
        _online.Status = publicly
            ? "LISTED UNDER BROWSE, AND STILL JOINABLE BY CODE"
            : "REACHED BY JOIN CODE ONLY, WHICH IS GATE ENOUGH";
        if (publicly) return;
        _online.Password.Set(string.Empty);
        if (!_online.Password.IsFocused) return;
        _online.Password.IsFocused = false;
        OnlineFields[0].IsFocused = true;
    }

    /// <summary>
    /// Turns the player's own overlord face forward or back through the atlas.
    /// </summary>
    /// <remarks>
    /// The same fifteen faces the local setup screen offers, cycled the same way. The sixteenth is
    /// the one drawn for a seat nobody is in, so it is not a face anybody plays under. The choice is
    /// only live while the form is, because it rides the request that claims the seat: once the
    /// seat is claimed the roster is what every client generates its city from.
    /// </remarks>
    private void CycleOnlinePortrait(int delta)
    {
        if (_online.Stage != MultiplayerStage.Connect) return;
        _online.Portrait = checked((short)Mod(
            _online.Portrait + delta, PlayerPortraitLayout.SelectableCount));
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
    }

    private void PasteJoinCode()
    {
        if (!DesktopClipboard.TryGetText(out var text, maximumCharacters: 8))
        {
            _online.Status = "THE CLIPBOARD DOES NOT CONTAIN TEXT";
            return;
        }
        _online.JoinCode.Set(text);
        foreach (var field in OnlineFields) field.IsFocused = false;
        _online.JoinCode.IsFocused = true;
        _online.Status = "JOIN CODE PASTED";
    }

    /// <summary>
    /// The password to send, or null for none.
    /// </summary>
    /// <remarks>
    /// The rule that a private session carries no password is enforced here rather than only where
    /// the field is drawn, so a value typed while the screen was showing something else cannot ride
    /// along into a lobby whose host has since been told there is no password on it.
    /// </remarks>
    private string? OptionalPassword() =>
        OnlinePasswordApplies && _online.Password.Value.Length > 0 ? _online.Password.Value : null;

    /// <summary>
    /// Refuses a name the original rules read as a cheat code before the server has to.
    /// </summary>
    /// <remarks>
    /// The server refuses these too, and its refusal is the one that counts, but saying so here
    /// turns a round trip into an immediate answer, and names which field is wrong while the player
    /// is still looking at it. See <see cref="ReservedPlayerNames"/> for why they cannot be allowed
    /// through: online, one player's name changes what every client computes.
    /// </remarks>
    private bool RequireUsableName()
    {
        var name = _online.DisplayName.Value.Trim();
        if (name.Length == 0)
        {
            _online.Status = "ENTER A NAME";
            return false;
        }
        if (ReservedPlayerNames.IsReserved(OriginalPlayerName.Project(name)))
        {
            _online.Status = "THAT NAME IS A CHEAT CODE  PICK ANOTHER";
            return false;
        }
        return true;
    }

    /// <summary>Sends focus back to the top of the form after a choice reshapes it.</summary>
    /// <remarks>
    /// Choosing a service or a role adds or removes fields, so the field that had the caret may no
    /// longer be on the screen. Focus has to land somewhere, or the next keystroke goes nowhere.
    /// </remarks>
    private void RefocusOnlineForm()
    {
        foreach (var field in new[]
                 {
                     _online.Server, _online.DisplayName, _online.SessionName,
                     _online.JoinCode, _online.Password
                 })
            field.IsFocused = false;
        OnlineFields[0].IsFocused = true;
        _online.Status = string.Empty;
    }
}
