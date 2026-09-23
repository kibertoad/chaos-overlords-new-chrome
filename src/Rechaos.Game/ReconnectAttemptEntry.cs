using System.Globalization;
using System.Text;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Game;

/// <summary>
/// One line of the reconnect log, and the whole diagnostic behind it.
/// </summary>
/// <remarks>
/// The line is what fits on the modal; the details are what a player pastes into a bug report. They
/// are built once, when the attempt is reported, so the exception is not kept alive by the log.
/// </remarks>
/// <param name="Attempt">Which attempt this was, counted from one.</param>
/// <param name="Summary">The upper-cased line the modal draws.</param>
/// <param name="Details">The full error of this attempt, copied verbatim.</param>
public sealed record ReconnectAttemptEntry(int Attempt, string Summary, string Details)
{
    /// <summary>Whether the server answered this attempt by limiting the client's request rate.</summary>
    public bool IsRateLimited { get; init; }

    public static ReconnectAttemptEntry From(
        MultiplayerNotice.ConnectionChanged connection, DateTimeOffset reportedAt)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var attempt = Math.Max(1, connection.Attempt);
        var detail = connection.Detail ?? string.Empty;
        var summary = $"ATTEMPT {attempt}  {detail}".ToUpperInvariant();

        var details = new StringBuilder()
            .Append("Reconnect attempt ").Append(attempt.ToString(CultureInfo.InvariantCulture)).AppendLine()
            .Append("Time: ")
            .Append(reportedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
            .AppendLine();
        if (connection.Lane is { Length: > 0 } lane) details.Append("Connection: ").Append(lane).AppendLine();
        details.Append("Summary: ").Append(detail);
        if (connection.Error is { } error) details.AppendLine().Append("Error:").AppendLine().Append(error.ToString());
        return new ReconnectAttemptEntry(attempt, summary, details.ToString())
        {
            IsRateLimited = MultiplayerFailureText.IsRateLimited(connection.Error),
        };
    }
}
