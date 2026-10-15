namespace RTUB.Application.DTOs;

/// <summary>
/// What happened to one push send (#230). <see cref="Sent"/> means the push service accepted it (2xx), not that the
/// device showed it: nothing reports the display back to the server.
/// </summary>
public enum PushDeliveryOutcome
{
    /// <summary>The push service accepted the message (2xx).</summary>
    Sent,
    /// <summary>404 / 410: the subscription no longer exists at the push service; the row was removed.</summary>
    Gone,
    /// <summary>Any other 4xx (400 bad request, 401/403 VAPID or JWT refused, 413 payload too large): nothing removed.</summary>
    Rejected,
    /// <summary>429 after the retries.</summary>
    RateLimited,
    /// <summary>5xx from the push service: transient, nothing removed.</summary>
    ProviderError,
    /// <summary>The push service could not be reached (DNS, TLS, socket) after the retries.</summary>
    NetworkError,
    /// <summary>Anything else, e.g. the server's VAPID keys are malformed and the request could not be built.</summary>
    Failed,
    /// <summary>Not sent: the VAPID keys are missing.</summary>
    NotConfigured,
    /// <summary>Not sent: the endpoint is not stored for this member.</summary>
    NotRegistered,
}

/// <summary>One push send's outcome and the push service's HTTP status when there was one.</summary>
public sealed record PushSendResult(PushDeliveryOutcome Outcome, int? StatusCode = null);

/// <summary>POST /api/push/test: what the server knows about the test push to this browser (#230).</summary>
public sealed record PushTestResultDto(string Outcome, int? Status);
