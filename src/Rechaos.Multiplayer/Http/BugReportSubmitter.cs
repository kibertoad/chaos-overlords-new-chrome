using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Http;

/// <summary>
/// Where bug reports go.
/// </summary>
/// <remarks>
/// <para>
/// Not the address in the online-play field, and not configurable. A report goes to the people who
/// maintain the game, and a player who is self-hosting a lobby for three friends is not them —
/// posting there would send a crash to somebody who cannot act on it and never send it to anybody
/// who can. It is the same server software and the same deployment that will host public matches,
/// reached over a route of its own; only the database behind that route is separate.
/// </para>
/// <para>
/// The public deployment does not exist yet, so this is a local one. Changing it is this one line.
/// </para>
/// </remarks>
public static class BugReportEndpoint
{
    /// <summary>The address reports are posted to.</summary>
    public static Uri Default { get; } = new("http://localhost:8787");
}

/// <summary>Why a report did not reach the server, in terms a player can be told.</summary>
public enum BugReportFailure
{
    /// <summary>Nothing answered: no network, or the server is not running.</summary>
    Unreachable,

    /// <summary>The server answered, and said it does not take reports.</summary>
    NotAccepted,

    /// <summary>Too many reports from here too quickly.</summary>
    TooMany,

    /// <summary>The server refused the report itself — too large, or malformed.</summary>
    Refused,

    /// <summary>The server failed while storing it.</summary>
    ServerError
}

/// <summary>A report that did not land, and why.</summary>
public sealed class BugReportException : Exception
{
    public BugReportException(BugReportFailure failure, string message, Exception? inner = null)
        : base(message, inner)
    {
        Failure = failure;
    }

    public BugReportFailure Failure { get; }
}

/// <summary>
/// Posts one bug report.
/// </summary>
/// <remarks>
/// Deliberately not part of <see cref="MultiplayerClient"/>. That client is a membership in a
/// match: it carries a bearer token, it is pointed at whichever server the player joined, and every
/// call on it belongs to a lobby. A bug report has no token, no match and a fixed destination, and
/// the one thing it must keep doing is working when none of that is available.
/// </remarks>
public sealed class BugReportSubmitter
{
    /// <summary>
    /// Generous, because the request is large and the player is waiting on nothing else.
    /// </summary>
    /// <remarks>
    /// A journal is megabytes. On a slow uplink the upload alone outlasts the fifteen seconds a
    /// lobby call is given, and a report abandoned at the last kilobyte is the worst of both: the
    /// player's uplink spent and nothing to show for it.
    /// </remarks>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(3);

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly TimeSpan _timeout;

    public BugReportSubmitter(HttpClient http, Uri? endpoint = null, TimeSpan? timeout = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        var address = endpoint ?? BugReportEndpoint.Default;
        _endpoint = new Uri(
            address.AbsolutePath.EndsWith('/')
                ? address
                : new Uri(address, $"{address.AbsolutePath}/"),
            $"{ApiRoutes.Prefix[1..]}{ApiRoutes.BugReports}");
        _timeout = timeout ?? DefaultTimeout;
    }

    /// <summary>Sends the report and answers the server's receipt.</summary>
    /// <exception cref="BugReportException">The report did not reach the server.</exception>
    public async Task<BugReportReceipt> SubmitAsync(
        SubmitBugReportRequest report,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(WireJson.Write(report), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);
        HttpResponseMessage response;
        try
        {
            response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, deadline.Token)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException
                                          or OperationCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            throw new BugReportException(
                BugReportFailure.Unreachable, "The bug report server did not answer.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode) throw Refusal(response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            try
            {
                return WireJson.Read<BugReportReceipt>(body);
            }
            catch (MultiplayerProtocolException exception)
            {
                throw new BugReportException(
                    BugReportFailure.ServerError,
                    "The bug report server answered something this build cannot read.",
                    exception);
            }
        }
    }

    /// <summary>
    /// The refusal, as one of the four outcomes a player can do something about.
    /// </summary>
    /// <remarks>
    /// The body is not read. It carries the same error envelope every route answers, and none of
    /// what is in it changes what the player should be told — so it is not parsed, which also means
    /// a server answering something unexpected cannot turn a refusal into a second failure.
    /// </remarks>
    private static BugReportException Refusal(HttpStatusCode status) => status switch
    {
        HttpStatusCode.NotFound => new BugReportException(
            BugReportFailure.NotAccepted, "That server does not accept bug reports."),
        HttpStatusCode.TooManyRequests => new BugReportException(
            BugReportFailure.TooMany, "Too many reports were sent from here; try again shortly."),
        HttpStatusCode.RequestEntityTooLarge
            or HttpStatusCode.UnprocessableEntity
            or HttpStatusCode.BadRequest => new BugReportException(
                BugReportFailure.Refused, "The server refused this report."),
        _ => new BugReportException(
            BugReportFailure.ServerError, $"The server failed to store the report ({(int)status}).")
    };
}
