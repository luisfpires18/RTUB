namespace RTUB.Application.DTOs;

/// <summary>
/// GET /api/push/status and the answer of every push write (task 038, docs/react-push-v2.md): the server's side of the
/// signed-in member's push state. <see cref="Reason"/> says why push is off ("disabled": WebPush:Enabled is false and
/// the member is not Owner; "not-configured": no VAPID keys), null when it is available. Only the VAPID public key
/// ever leaves the server. <see cref="Subscriptions"/> counts the member's own subscribed devices / browsers.
/// </summary>
public sealed record PushStatusDto(bool Enabled, bool Configured, string? Reason, string? VapidPublicKey, bool OptedOut, int Subscriptions);

/// <summary>
/// POST /api/push/subscribe: the browser's <c>PushSubscription.toJSON()</c> (expirationTime in Unix milliseconds or
/// null), plus the endpoint this one replaces on the same device (a key rotation or a repair), removed in the same call.
/// </summary>
public sealed record PushSubscribeInput(string? Endpoint, PushKeysDto? Keys, long? ExpirationTime, string? Replaces);

/// <summary>POST /api/push/check: whether this browser's endpoint is stored for the member.</summary>
public sealed record PushEndpointInput(string? Endpoint);

/// <summary>POST /api/push/unsubscribe: this browser's endpoint, or <see cref="All"/> for every device of the member.</summary>
public sealed record PushUnsubscribeInput(string? Endpoint, bool All);
