using System.Text.Json;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    private sealed record RecordedOrder(int Turn, int Slot, int Action, int Target, int Target2, bool Repeat)
    {
        // "turn 3: gang slot 0 action 13 target 0 target_2 0 repeat 0", as the probe writes it.
        public static RecordedOrder Parse(string value)
        {
            var match = System.Text.RegularExpressions.Regex.Match(value,
                @"^turn (-?\d+): gang slot (-?\d+) action (-?\d+) target (-?\d+) target_2 (-?\d+) repeat ([01])$");
            Assert.True(match.Success, value);
            var numbers = match.Groups.Values.Skip(1)
                .Select(group => int.Parse(group.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5] != 0);
        }
    }

    private sealed record RecordedPlanning(int Turn, int Player, int Slot, int Family, bool Raider, bool Retired = false, int? Cash = null)
    {
        // "turn 2: player 1 gang slot 0 family 4", "turn 1: player 3 raider_mode 1", "turn 1:
        // player 3 player_active 0" or "turn 1: player 0 cash 30000", as the probe writes them.
        public static RecordedPlanning Parse(string value)
        {
            var cash = System.Text.RegularExpressions.Regex.Match(value, @"^turn (\d+): player ([0-5]) cash (-?\d+)$");
            if (cash.Success)
                return new(int.Parse(cash.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                    int.Parse(cash.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), 0, 0, false,
                    Cash: int.Parse(cash.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));
            var retired = System.Text.RegularExpressions.Regex.Match(value, @"^turn (\d+): player ([0-5]) player_active 0$");
            if (retired.Success)
                return new(int.Parse(retired.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                    int.Parse(retired.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), 0, 0, false, true);
            var raider = System.Text.RegularExpressions.Regex.Match(value, @"^turn (\d+): player ([1-5]) raider_mode 1$");
            if (raider.Success)
                return new(int.Parse(raider.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                    int.Parse(raider.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), 0, 0, true);
            var match = System.Text.RegularExpressions.Regex.Match(value,
                @"^turn (\d+): player ([1-5]) gang slot (\d+) family (\d+)$");
            Assert.True(match.Success, value);
            var numbers = match.Groups.Values.Skip(1)
                .Select(group => int.Parse(group.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new(numbers[0], numbers[1], numbers[2], numbers[3], false);
        }
    }

    // The three arguments the endgame renderer was called with, the players of the names its first
    // drawing listed, in drawing order, and each row's kind: splash, ranked or eliminated
    // (FND-AWARDS-005).
    private sealed record RecordedEndgame(IReadOnlyList<int> Arguments, IReadOnlyList<int> Players, IReadOnlyList<string> Kinds)
    {
        // FND-AWARDS-005: the renderer draws the victory splash of the player in its third argument
        // when its first argument is not 0 and its second is 0, and the table otherwise.
        public bool DrawsSplash => Arguments[0] != 0 && Arguments[1] == 0;
    }
    // The viewer of the last city redraw and its site markers as definition, sector, ordinal and
    // controlled flag (FND-SEARCH-006).
    private sealed record RecordedMarkers(int Viewer, IReadOnlyList<int[]> Markers);

    // The site definitions whose search_filters entries the probe set for the first human before
    // the Done press of the turn (RULE-SEARCH-001).
    private sealed record RecordedSearch(int Turn, IReadOnlyList<int> Definitions)
    {
        // "turn 1: search filter 0 2 4", as the probe writes it.
        public static RecordedSearch Parse(string value)
        {
            var match = System.Text.RegularExpressions.Regex.Match(value, @"^turn (\d+): search filter (\d+(?: \d+)*)$");
            Assert.True(match.Success, value);
            return new(int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                match.Groups[2].Value.Split(' ')
                    .Select(definition => int.Parse(definition, System.Globalization.CultureInfo.InvariantCulture)).ToArray());
        }
    }

    // A Financial panel the probe opened before the Done press of Turn, with the sector its function
    // was passed (-1 for the City variant) and the nine numbers it drew (FND-FINANCE-003).
    private sealed record RecordedFinance(int Turn, int Sector, IReadOnlyList<int> Values);

    // The list the original's Equip list builder filled for one category of one of the human's
    // gangs at the recording's endpoint, with the Tech Level it was passed (FND-EQUIP-008).
    private sealed record RecordedEquipList(int Slot, int Category, int TechLevel, IReadOnlyList<int> Items);

    // The opponent's roster slots the original's Attack picker roster builder listed for one of the
    // human's gangs at the recording's endpoint, with the sector it was passed (FND-ATTACK-006).
    private sealed record RecordedAttackList(int Slot, int Sector, int Opponent, IReadOnlyList<int> Targets);

    // A clip Detailed Combat played (FND-COMBAT-011): the roll count before it, the focal and other
    // element numbers (-2 the police), the clip player's argument, the two bars' right ends, the
    // sound numbers loaded into slot 5 since the clip before, and whether it played slot 5.
    private sealed record RecordedCombatClip(
        int AfterRoll, int Focal, int Other, int Hold, int FocalBar, int OtherBar, IReadOnlyList<int> Sounds, bool Played);

    // A call of the Detailed Combat presentation (FND-COMBAT-010): the roll count before it, 1 when
    // planning opened it and 0 when the console's control did, its first clip in the clip list, the
    // clips it started, the effect slots it played itself and whether it returned before the run
    // ended; a fixture recorded before the probe kept that has only presentations that returned.
    private sealed record RecordedCombatPresentation(
        int AfterRoll, bool Automatic, int FirstClip, int Clips, IReadOnlyList<int> Sounds, bool Returned);

    // A call of the cursor helper (FND-UI-034): the roll count and the Done presses before it, the
    // shape and force it was passed, and the address of the call.
    private sealed record RecordedPointerCall(int AfterRoll, int Done, int Shape, int Force, int Call);

    // A call of the play helper (FND-AUDIO-006) or of the effects wrapper (FND-AUDIO-002): the roll
    // count and the Done presses before it, the effect slot and the address of the call.
    private sealed record RecordedSoundCall(int AfterRoll, int Done, int Slot, int Call);

    // A call of the level setup (FND-AUDIO-019): the roll count and the Done presses before it, the
    // address of the call, and whether effects_enabled was set when it returned.
    private sealed record RecordedLevelSetup(int AfterRoll, int Done, int Call, bool EffectsEnabled);

    // A call of the presentation wait (FND-TIMER-002): its argument, the call's address, and the
    // milliseconds of its start and return on the clock the run's ticks are timed with.
    private sealed record RecordedWait(int Ticks, int Call, long Started, long Returned);

    // A slide-in of the panel-open helper (FND-UI-011): the benchmark count it read, its travel and
    // the offset of each copy.
    private sealed record RecordedSlide(int Benchmark, int Travel, IReadOnlyList<int> Offsets);

    // A movie the intro played (FND-VIDEO-002): its name, its header's frame count, the movie
    // slot's frame counter at each frame shown, the milliseconds from the first movie's first frame
    // to each, and the counter when the slot was closed.
    private sealed record RecordedIntroMovie(
        string Name, int Frames, IReadOnlyList<int> Shown, IReadOnlyList<long> Milliseconds, int ClosedAt);

    // A hire step the probe took after the dump, an offer dragged onto a sector, its Reject pressed
    // (sector -2) or a result panel's Exit pressed (slot -1), with hire_orders after it
    // (FND-HIRE-001, FND-HIRE-008).
    private sealed record RecordedHireStep(int Slot, int Sector, IReadOnlyList<int> Orders);
    private sealed record RecordedSavedWrite(int Turn, int Before, int Value);
    private sealed record RecordedClose(
        int Saved, int NoMatch, int Answer, int SaveResult, IReadOnlyList<int> Dialogs, int Saves, bool Left);

    // A click the probe posted after the dump, whether the Search panel was open after it, the
    // active player and the whole search_filters table (FND-SEARCH-001, FND-SEARCH-002).
    private sealed record RecordedSearchClick(int X, int Y, bool PanelOpen, int ActivePlayer, IReadOnlyList<int> Filters);

    // An order step the probe took after the dump (RULE-TURN-005, SCR-UI-004): a double-click on
    // city sector Target (open), a press at (X, Y) within card Target (card) or of the window
    // (strip) answered with command Choice, or a press of the back control or of a result panel's
    // Exit. After it: the popup menu it opened (-1 for none) with each item's command and greyed
    // state, whether the city view is shown, the six card slots, and for every gang of the active
    // player in use, slot 80 with them, its slot, sector, action, target, target_2, repeat_action
    // and repeat_target (FMT-STATE-001, FND-UI-021), and the player whose gangs the cards list
    // (FND-UI-015), -1 where the run did not keep it.
    private sealed record RecordedOrderStep(
        string Kind, int Target, int X, int Y, int Choice, int Menu, IReadOnlyList<(int Command, int State)>? Items,
        bool CityView, IReadOnlyList<int> Cards, IReadOnlyList<IReadOnlyList<int>> Gangs, int Viewed);

    // A call of a planning entry panel: its name, the roll count when it was called and whether it
    // stayed open until Exit was pressed (RULE-SETUP-008).
    private sealed record RecordedPanel(string Panel, int AfterRoll, bool Shown);

    // The planning clock of a turn that ran out: the limit, each redraw of the bar as elapsed
    // milliseconds, width and warning slot (0 for none), and the elapsed milliseconds of the last
    // time-limit test that let planning go on and of the one that ended it (RULE-TIMER-002).
    private sealed record RecordedTimer(
        int Turn, int LimitMs, IReadOnlyList<(int Elapsed, int Width, int Slot)> Bars, int LastUnexpiredMs, int ExpiredMs);

    private sealed record RecordedHire(int Turn, int OfferSlot, int Sector)
    {
        // "turn 1: offer slot 0 sector 12", as the probe writes it.
        public static RecordedHire Parse(string value)
        {
            var match = System.Text.RegularExpressions.Regex.Match(value,
                @"^turn (\d+): offer slot ([0-2]) sector (\d+)$");
            Assert.True(match.Success, value);
            var numbers = match.Groups.Values.Skip(1)
                .Select(group => int.Parse(group.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new(numbers[0], numbers[1], numbers[2]);
        }
    }

    private sealed class RecordedRun
    {
        private readonly Dictionary<(string, int), int> _terms = [];
        private readonly Dictionary<(string, int, string), int> _fields = [];

        public RecordedRun(JsonElement run, JsonElement inputs)
        {
            Orders = inputs.EnumerateArray()
                .Where(input => input.GetProperty("name").GetString() == "order")
                .Select(input => RecordedOrder.Parse(input.GetProperty("value").GetString()!))
                .ToArray();
            Hires = inputs.EnumerateArray()
                .Where(input => input.GetProperty("name").GetString() == "hire")
                .Select(input => RecordedHire.Parse(input.GetProperty("value").GetString()!))
                .ToArray();
            Planning = inputs.EnumerateArray()
                .Where(input => input.GetProperty("name").GetString() == "planning")
                .Select(input => RecordedPlanning.Parse(input.GetProperty("value").GetString()!))
                .ToArray();
            Seed = run.GetProperty("rng_state").GetInt32();
            // "planning_limit_choice 1", as the probe writes a setup choice.
            PlanningLimitChoice = inputs.EnumerateArray()
                .Where(input => input.GetProperty("name").GetString() == "setup")
                .Select(input => input.GetProperty("value").GetString()!)
                .Where(value => value.StartsWith("planning_limit_choice ", StringComparison.Ordinal))
                .Select(value => int.Parse(value["planning_limit_choice ".Length..], System.Globalization.CultureInfo.InvariantCulture))
                .FirstOrDefault();
            // "no Done press, turn 2: the planning time runs out", as the probe writes a turn it
            // left to the planning time limit.
            ExpiredTurns = inputs.EnumerateArray()
                .Where(input => input.GetProperty("name").GetString() == "wait")
                .Select(input => input.GetProperty("value").GetString()!)
                .Where(value => value.StartsWith("no Done press, turn ", StringComparison.Ordinal))
                .Select(value => int.Parse(value["no Done press, turn ".Length..value.IndexOf(':')], System.Globalization.CultureInfo.InvariantCulture))
                .ToHashSet();
            Timers = run.TryGetProperty("timers", out var timers)
                ? timers.EnumerateArray().Select(timer => new RecordedTimer(
                    timer.GetProperty("turn").GetInt32(), timer.GetProperty("limit_ms").GetInt32(),
                    timer.GetProperty("bars").EnumerateArray()
                        .Select(bar => (bar[0].GetInt32(), bar[1].GetInt32(), bar[2].GetInt32())).ToArray(),
                    timer.GetProperty("last_unexpired_ms").GetInt32(), timer.GetProperty("expired_ms").GetInt32())).ToArray()
                : [];
            DoneAtRoll = run.TryGetProperty("done_at_roll", out var doneAt)
                ? doneAt.EnumerateArray().Select(value => value.GetInt32()).ToArray()
                : [];
            Panels = run.TryGetProperty("panels", out var panels)
                ? panels.EnumerateArray().Select(call => new RecordedPanel(
                    call.GetProperty("panel").GetString()!, call.GetProperty("after_roll").GetInt32(),
                    call.GetProperty("shown").GetBoolean())).ToArray()
                : null;
            EndgameRows = run.TryGetProperty("endgame_rows", out var endgame)
                ? new RecordedEndgame(
                    endgame.GetProperty("arguments").EnumerateArray().Select(value => value.GetInt32()).ToArray(),
                    endgame.GetProperty("players").EnumerateArray().Select(value => value.GetInt32()).ToArray(),
                    endgame.GetProperty("kinds").EnumerateArray().Select(value => value.GetString()!).ToArray())
                : null;
            CityMarkers = run.TryGetProperty("city_markers", out var markers)
                ? new RecordedMarkers(markers.GetProperty("viewer").GetInt32(),
                    markers.GetProperty("markers").EnumerateArray()
                        .Select(marker => marker.EnumerateArray().Select(value => value.GetInt32()).ToArray()).ToArray())
                : null;
            HireSteps = run.TryGetProperty("hire_steps", out var hireSteps)
                ? hireSteps.EnumerateArray().Select(step => new RecordedHireStep(
                    step.GetProperty("slot").GetInt32(), step.GetProperty("sector").GetInt32(),
                    step.GetProperty("orders").EnumerateArray().Select(value => value.GetInt32()).ToArray())).ToArray()
                : [];
            SavedWrites = run.TryGetProperty("saved_writes", out var savedWrites)
                ? savedWrites.EnumerateArray().Select(write => new RecordedSavedWrite(
                    write.GetProperty("turn").GetInt32(), write.GetProperty("before").GetInt32(),
                    write.GetProperty("value").GetInt32())).ToArray()
                : [];
            Closes = run.TryGetProperty("closes", out var closes)
                ? closes.EnumerateArray().Select(close => new RecordedClose(
                    close.GetProperty("saved").GetInt32(), close.GetProperty("no_match").GetInt32(),
                    close.GetProperty("answer").GetInt32(), close.GetProperty("save_result").GetInt32(),
                    close.GetProperty("dialogs").EnumerateArray().Select(value => value.GetInt32()).ToArray(),
                    close.GetProperty("saves").GetInt32(),
                    close.GetProperty("left_at").GetString() != "0x00000000")).ToArray()
                : [];
            GangMarkers = run.TryGetProperty("gang_markers", out var gangMarkers)
                ? gangMarkers.EnumerateArray()
                    .Select(draw => (IReadOnlyList<int>)draw.EnumerateArray().Select(value => value.GetInt32()).ToArray())
                    .ToArray()
                : [];
            OrderSteps = run.TryGetProperty("order_steps", out var orderSteps)
                ? orderSteps.EnumerateArray().Select(step => new RecordedOrderStep(
                    step.GetProperty("kind").GetString()!, step.GetProperty("target").GetInt32(),
                    step.GetProperty("x").GetInt32(), step.GetProperty("y").GetInt32(),
                    step.GetProperty("choice").GetInt32(), step.GetProperty("menu").GetInt32(),
                    step.TryGetProperty("items", out var items)
                        ? items.EnumerateArray().Select(item => (item[0].GetInt32(), item[1].GetInt32())).ToArray()
                        : null,
                    step.GetProperty("city_view").GetBoolean(),
                    step.GetProperty("cards").EnumerateArray().Select(value => value.GetInt32()).ToArray(),
                    step.GetProperty("gangs").EnumerateArray()
                        .Select(gang => (IReadOnlyList<int>)gang.EnumerateArray().Select(value => value.GetInt32()).ToArray())
                        .ToArray(),
                    step.TryGetProperty("viewed", out var viewed) ? viewed.GetInt32() : -1)).ToArray()
                : [];
            SearchClicks = run.TryGetProperty("search_clicks", out var searchClicks)
                ? searchClicks.EnumerateArray().Select(click => new RecordedSearchClick(
                    click.GetProperty("x").GetInt32(), click.GetProperty("y").GetInt32(),
                    click.GetProperty("panel_open").GetBoolean(), click.GetProperty("active_player").GetInt32(),
                    click.GetProperty("filters").EnumerateArray().Select(value => value.GetInt32()).ToArray())).ToArray()
                : [];
            Finance = run.TryGetProperty("finance", out var finance)
                ? finance.EnumerateArray().Select(panel => new RecordedFinance(
                    panel.GetProperty("turn").GetInt32(), panel.GetProperty("sector").GetInt32(),
                    panel.GetProperty("values").EnumerateArray().Select(value => value.GetInt32()).ToArray())).ToArray()
                : [];
            EquipLists = run.TryGetProperty("equip_lists", out var equipLists)
                ? equipLists.EnumerateArray().Select(list => new RecordedEquipList(
                    list.GetProperty("slot").GetInt32(), list.GetProperty("category").GetInt32(),
                    list.GetProperty("tech_level").GetInt32(),
                    list.GetProperty("items").EnumerateArray().Select(item => item.GetInt32()).ToArray())).ToArray()
                : [];
            AttackLists = run.TryGetProperty("attack_lists", out var attackLists)
                ? attackLists.EnumerateArray().Select(list => new RecordedAttackList(
                    list.GetProperty("slot").GetInt32(), list.GetProperty("sector").GetInt32(),
                    list.GetProperty("opponent").GetInt32(),
                    list.GetProperty("targets").EnumerateArray().Select(target => target.GetInt32()).ToArray())).ToArray()
                : [];
            CombatClips = run.TryGetProperty("combat_clips", out var combatClips)
                ? combatClips.EnumerateArray().Select(clip => new RecordedCombatClip(
                    clip.GetProperty("after_roll").GetInt32(), clip.GetProperty("focal").GetInt32(),
                    clip.GetProperty("other").GetInt32(), clip.GetProperty("hold").GetInt32(),
                    clip.GetProperty("focal_bar").GetInt32(), clip.GetProperty("other_bar").GetInt32(),
                    clip.GetProperty("sounds").EnumerateArray().Select(sound => sound.GetInt32()).ToArray(),
                    clip.GetProperty("played").GetBoolean())).ToArray()
                : null;
            CombatPresentations = run.TryGetProperty("combat_presentations", out var combatPresentations)
                ? combatPresentations.EnumerateArray().Select(presentation => new RecordedCombatPresentation(
                    presentation.GetProperty("after_roll").GetInt32(), presentation.GetProperty("automatic").GetInt32() != 0,
                    presentation.GetProperty("first_clip").GetInt32(), presentation.GetProperty("clips").GetInt32(),
                    presentation.GetProperty("sounds").EnumerateArray().Select(sound => sound.GetInt32()).ToArray(),
                    !presentation.TryGetProperty("returned", out var returned) || returned.GetBoolean())).ToArray()
                : null;
            PointerCalls = run.TryGetProperty("pointer_calls", out var pointerCalls)
                ? pointerCalls.EnumerateArray().Select(call => new RecordedPointerCall(
                    call[0].GetInt32(), call[1].GetInt32(), call[2].GetInt32(), call[3].GetInt32(), call[4].GetInt32())).ToArray()
                : null;
            SoundCalls = run.TryGetProperty("sound_calls", out var soundCalls)
                ? soundCalls.EnumerateArray().Select(call => new RecordedSoundCall(
                    call[0].GetInt32(), call[1].GetInt32(), call[2].GetInt32(), call[3].GetInt32())).ToArray()
                : null;
            EffectsEnabled = run.TryGetProperty("effects_enabled", out var effectsEnabled) ? effectsEnabled.GetBoolean() : null;
            EffectCalls = run.TryGetProperty("effect_calls", out var effectCalls)
                ? effectCalls.EnumerateArray().Select(call => new RecordedSoundCall(
                    call[0].GetInt32(), call[1].GetInt32(), call[2].GetInt32(), call[3].GetInt32())).ToArray()
                : null;
            LevelSetups = run.TryGetProperty("level_setups", out var levelSetups)
                ? levelSetups.EnumerateArray().Select(setup => new RecordedLevelSetup(
                    setup[0].GetInt32(), setup[1].GetInt32(), setup[2].GetInt32(), setup[3].GetInt32() != 0)).ToArray()
                : null;
            IntroMovies = run.TryGetProperty("intro_movies", out var introMovies)
                ? introMovies.EnumerateArray().Select(movie => new RecordedIntroMovie(
                    movie.GetProperty("name").GetString()!, movie.GetProperty("frames").GetInt32(),
                    movie.GetProperty("shown").EnumerateArray().Select(value => value.GetInt32()).ToArray(),
                    movie.GetProperty("milliseconds").EnumerateArray().Select(value => value.GetInt64()).ToArray(),
                    movie.GetProperty("closed_at").GetInt32())).ToArray()
                : null;
            Ticks = run.TryGetProperty("ticks", out var ticks)
                ? ticks.EnumerateArray().Select(tick => tick.GetInt64()).ToArray()
                : null;
            Waits = run.TryGetProperty("waits", out var waits)
                ? waits.EnumerateArray().Select(wait => new RecordedWait(
                    wait[0].GetInt32(), wait[1].GetInt32(), wait[2].GetInt64(), wait[3].GetInt64())).ToArray()
                : null;
            Slides = run.TryGetProperty("slides", out var slides)
                ? slides.EnumerateArray().Select(slide => new RecordedSlide(
                    slide.GetProperty("benchmark").GetInt32(), slide.GetProperty("travel").GetInt32(),
                    slide.GetProperty("offsets").EnumerateArray().Select(value => value.GetInt32()).ToArray())).ToArray()
                : null;
            // The inputs list every turn up to --end-turns, but a match that ends early presses
            // Done fewer times, and the probe writes a turn's filter entries only before its press.
            SearchFilter = inputs.EnumerateArray()
                .Where(input => input.GetProperty("name").GetString() == "search")
                .Select(input => RecordedSearch.Parse(input.GetProperty("value").GetString()!))
                .Where(write => write.Turn <= DoneCount)
                .SelectMany(write => write.Definitions)
                .Distinct().ToArray();
            Rolls = run.GetProperty("rolls").EnumerateArray()
                .Select(roll => (roll[0].GetString()!, roll[1].GetInt32(), roll[2].GetInt32()))
                .ToArray();
            RollsAtDump = run.TryGetProperty("rolls_at_dump", out var atDump) ? atDump.GetInt32() : Rolls.Count;
            foreach (var row in run.GetProperty("end_state").EnumerateArray())
            {
                var value = row.GetProperty("value").GetInt32();
                if (row.TryGetProperty("term", out var term))
                    _terms[(term.GetString()!, row.GetProperty("index").GetInt32())] = value;
                else
                    _fields[(row.GetProperty("format").GetString()!, row.GetProperty("record").GetInt32(),
                        row.GetProperty("field").GetString()!)] = value;
            }
        }

        public int Seed { get; }
        public int DoneCount => DoneAtRoll.Count;
        public IReadOnlyList<int> DoneAtRoll { get; }
        // The turns whose planning time ran out with no Done press; DoneAtRoll still has an entry
        // for each.
        public IReadOnlySet<int> ExpiredTurns { get; }

        /// <summary>
        /// The rolls the state dump follows; steps after the dump, such as a Ready press that refills
        /// the offers (RULE-SETUP-008), can make more.
        /// </summary>
        public int RollsAtDump { get; }
        public int PlanningLimitChoice { get; }
        public IReadOnlyList<RecordedTimer> Timers { get; }
        public IReadOnlyList<RecordedPanel>? Panels { get; }
        public RecordedEndgame? EndgameRows { get; }
        public IReadOnlyList<int> SearchFilter { get; }
        public RecordedMarkers? CityMarkers { get; }
        public IReadOnlyList<RecordedFinance> Finance { get; }
        public IReadOnlyList<RecordedEquipList> EquipLists { get; }
        public IReadOnlyList<RecordedAttackList> AttackLists { get; }
        // Null when the run left Detailed Combat switched off.
        public IReadOnlyList<RecordedCombatClip>? CombatClips { get; }
        // Null for a run recorded before the probe kept the presentation calls.
        public IReadOnlyList<RecordedCombatPresentation>? CombatPresentations { get; }
        // Null when the run did not record the pointer.
        public IReadOnlyList<RecordedPointerCall>? PointerCalls { get; }
        // Null when the run did not record the play helper.
        public IReadOnlyList<RecordedSoundCall>? SoundCalls { get; }
        // Whether effects_enabled was set at each read the probe made (FND-AUDIO-002), which decides
        // whether the effects wrapper's calls reach the play helper. Null when the run did not
        // record it.
        public bool? EffectsEnabled { get; }
        // Null when the run did not record the effects wrapper.
        public IReadOnlyList<RecordedSoundCall>? EffectCalls { get; }
        // Null when the run did not record the level setup.
        public IReadOnlyList<RecordedLevelSetup>? LevelSetups { get; }
        // Null when the run did not record the presentation clock: the milliseconds of each tick of
        // timer slot 0 from the dump on, and each call of the wait.
        public IReadOnlyList<long>? Ticks { get; }
        public IReadOnlyList<RecordedWait>? Waits { get; }
        // Null when the run did not record the slides.
        public IReadOnlyList<RecordedSlide>? Slides { get; }
        // Null when the run held the button through the intro.
        public IReadOnlyList<RecordedIntroMovie>? IntroMovies { get; }
        public IReadOnlyList<RecordedSearchClick> SearchClicks { get; }
        public IReadOnlyList<RecordedHireStep> HireSteps { get; }
        public IReadOnlyList<RecordedSavedWrite> SavedWrites { get; }
        public IReadOnlyList<RecordedClose> Closes { get; }
        public IReadOnlyList<RecordedOrderStep> OrderSteps { get; }

        // Each gang-status marker drawing from the last full city redraw before the dump on, as
        // the post-dump step it came in (0 before the first), kind, player, sector and frame:
        // kind 0 a full redraw, 1 a frame drawn for a sector holding the player's gang, 2 the
        // cell under the incoming-only mark copied back over the sector, 3 that mark drawn
        // (FND-UI-024).
        public IReadOnlyList<IReadOnlyList<int>> GangMarkers { get; }
        public IReadOnlyList<RecordedOrder> Orders { get; }
        public IReadOnlyList<RecordedHire> Hires { get; }
        public IReadOnlyList<RecordedPlanning> Planning { get; }

        // controller: 0 for a human at this computer (FND-SETUP-002), -2 for one eliminated who has
        // not yet seen the card and -1 for one who has (RULE-OBJECTIVE-005). Setup fills every
        // empty slot with a computer player (FND-SETUP-002), so a -1 here is always a retired human.
        public IReadOnlyList<PlayerId> Humans => Enumerable.Range(0, 6)
            .Where(slot => Term("controller", slot) is 0 or -1 or -2).Select(slot => new PlayerId(slot)).ToArray();
        public IReadOnlyList<(string Call, int Bound, int Result)> Rolls { get; }

        public int Term(string term, int index) => _terms[(term, index)];

        public int Sector(int sector, string field) => _fields[("FMT-STATE-002", sector, field)];

        public int Gang(int record, string field) => _fields[("FMT-STATE-001", record, field)];

        public bool HasTerm(string term, int index) => _terms.ContainsKey((term, index));

        public int Field(string format, int record, string field) => _fields[(format, record, field)];

        public bool HasField(string format, int record, string field) => _fields.ContainsKey((format, record, field));

        /// <summary>Every value the fixture records for <paramref name="format"/>.</summary>
        public IEnumerable<int> Values(string format) =>
            _fields.Where(entry => entry.Key.Item1 == format).Select(entry => entry.Value);

        /// <summary>A field or term of a sparse table, which the fixture leaves out when it holds 0.</summary>
        public int FieldOrZero(string format, int record, string field) => _fields.GetValueOrDefault((format, record, field));

        /// <inheritdoc cref="FieldOrZero"/>
        public int TermOrZero(string term, int index) => _terms.GetValueOrDefault((term, index));

        /// <summary>FMT-STATE-006: report <paramref name="index"/> of a player's Last Turn reports.</summary>
        public LastTurnReportRecord Report(int player, int index)
        {
            var record = player * 32 + index;
            return new(_fields[("FMT-STATE-006", record, "report_type")], _fields[("FMT-STATE-006", record, "arg1")],
                _fields[("FMT-STATE-006", record, "arg2")], _fields[("FMT-STATE-006", record, "arg3")]);
        }

        public IReadOnlyList<int> GangRecords(int player) => _fields.Keys
            .Where(key => key.Item1 == "FMT-STATE-001" && key.Item3 == "player" && key.Item2 / 81 == player)
            .Select(key => key.Item2).Order().ToArray();
    }
}
