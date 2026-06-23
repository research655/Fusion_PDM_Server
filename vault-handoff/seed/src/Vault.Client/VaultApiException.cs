using System.Net;

namespace Vault.Client;

/// <summary>
/// Thrown for any non-success API response, carrying the HTTP status so callers can branch:
/// 400 bad input, 403 not allowed, 404 not found, 409 conflict, 422 invalid action.
/// </summary>
public sealed class VaultApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public VaultApiException(HttpStatusCode status, string message) : base(message) => StatusCode = status;
}
