using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

public sealed class GameState
{
    public const int BoardSize = MatchLimits.BoardWidth;
    public const int MaximumPlayers = MatchLimits.PlayerCount;
    public const int MaximumGangsPerPlayer = MatchLimits.GangsPerPlayer;
    public const int MaximumFriendlyGangsPerSector = MatchLimits.FriendlyGangsPerSector;
    private readonly Random _random;
    private readonly OriginalData _data;
    private int _nextGangId;

    public GameState(OriginalData data, int seed = 1996)
    {
        _data = data;
        _random = new Random(seed);
        Players = Enumerable.Range(0, MaximumPlayers).Select(i => new PlayerState(i, $"PLAYER {i + 1}", 500)).ToArray();
        Sectors = Enumerable.Range(0, BoardSize * BoardSize).Select(CreateSector).ToArray();
        CurrentPlayer = 0;
        Cursor = 0;
        Message = "TAKE THE CITY. ENTER: CONTROL  H: HIRE  SPACE: END TURN";
    }

    public SectorState[] Sectors { get; }
    public PlayerState[] Players { get; }
    public int CurrentPlayer { get; private set; }
    public int Cursor { get; private set; }
    public int Turn { get; private set; } = 1;
    public string Message { get; private set; }
    public TurnCommandQueue Commands { get; } = new();

    public void MoveCursor(int dx, int dy)
    {
        var x = Math.Clamp(Cursor % BoardSize + dx, 0, BoardSize - 1);
        var y = Math.Clamp(Cursor / BoardSize + dy, 0, BoardSize - 1);
        Cursor = y * BoardSize + x;
        Message = Sectors[Cursor].Summary;
    }

    public bool TakeControl()
    {
        var sector = Sectors[Cursor];
        var player = Players[CurrentPlayer];
        var cost = 25 + sector.Resistance * 5;
        if (player.Cash < cost)
        {
            Message = $"INSUFFICIENT CASH: ${cost} REQUIRED";
            return false;
        }
        player.Cash -= cost;
        sector.Owner = CurrentPlayer;
        Message = $"{player.Name} CONTROLS SECTOR {Cursor + 1}";
        return true;
    }

    public bool HireGang()
    {
        var player = Players[CurrentPlayer];
        if (player.Gangs.Count >= MaximumGangsPerPlayer) { Message = "GANG CAPACITY REACHED"; return false; }
        if (player.Gangs.Count(gang => gang.Sector == Cursor) >= MaximumFriendlyGangsPerSector)
        {
            Message = "SECTOR GANG CAPACITY REACHED";
            return false;
        }
        var choices = _data.Gangs.Where(g => g.Force > 0 && g.Force <= player.Cash).ToArray();
        if (choices.Length == 0) { Message = "NO AFFORDABLE GANGS"; return false; }
        var gang = choices[_random.Next(choices.Length)];
        player.Cash -= gang.Force;
        player.Gangs.Add(new GangState(new GangId(_nextGangId++), new PlayerId(CurrentPlayer), gang, Cursor));
        Message = $"HIRED {gang.Name} FOR ${gang.Force}";
        return true;
    }

    public bool QueueCommand(GangId gangId, GangAction action, CommandTarget target, bool repeat = false)
    {
        var player = Players[CurrentPlayer];
        var gang = player.Gangs.SingleOrDefault(candidate => candidate.Id == gangId);
        if (gang is null)
        {
            Message = "GANG IS NOT CONTROLLED BY CURRENT PLAYER";
            return false;
        }
        if (action == GangAction.None)
        {
            Message = "SELECT A COMMAND";
            return false;
        }

        Commands.Set(new GameCommand(gang.Owner, gang.Id, action, target, repeat));
        Message = $"{gang.Definition.Name}: {action.ToString().ToUpperInvariant()} QUEUED";
        return true;
    }

    public bool CancelCommand(GangId gangId)
    {
        var player = Players[CurrentPlayer];
        if (player.Gangs.All(candidate => candidate.Id != gangId))
        {
            Message = "GANG IS NOT CONTROLLED BY CURRENT PLAYER";
            return false;
        }

        var cancelled = Commands.Cancel(gangId);
        Message = cancelled ? "COMMAND CANCELLED" : "GANG HAS NO COMMAND";
        return cancelled;
    }

    public void EndTurn()
    {
        var player = Players[CurrentPlayer];
        var income = Sectors.Where(s => s.Owner == CurrentPlayer).Sum(s => s.Income);
        var upkeep = player.Gangs.Sum(g => g.Definition.Upkeep);
        player.Cash = Math.Max(0, player.Cash + income - upkeep);
        CurrentPlayer = (CurrentPlayer + 1) % Players.Length;
        if (CurrentPlayer == 0) Turn++;
        Message = $"TURN {Turn}: {Players[CurrentPlayer].Name}";
    }

    private SectorState CreateSector(int index)
    {
        var sites = Enumerable.Range(0, 3).Select(_ => _data.Sites[_random.Next(_data.Sites.Count)]).ToArray();
        return new SectorState(index, sites);
    }
}

public sealed class SectorState
{
    public SectorState(int id, SiteDefinition[] sites)
    {
        Id = id;
        Sites = sites;
        Income = Math.Max(1, sites.Sum(site => site.Cash));
        Resistance = Math.Max(1, sites.Sum(site => site.Resistance));
    }
    public int Id { get; }
    public SiteDefinition[] Sites { get; }
    public int Owner { get; set; } = -1;
    public int Income { get; }
    public int Resistance { get; }
    public string Summary => $"SECTOR {Id + 1}  ${Income}/TURN  RESIST {Resistance}  {string.Join(", ", Sites.Select(s => s.Name))}";
}

public sealed class PlayerState(int id, string name, int cash)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public int Cash { get; set; } = cash;
    public List<GangState> Gangs { get; } = [];
}

public sealed class GangState(GangId id, PlayerId owner, GangDefinition definition, int sector)
{
    public GangId Id { get; } = id;
    public PlayerId Owner { get; } = owner;
    public GangDefinition Definition { get; } = definition;
    public int Sector { get; internal set; } = sector;
}
