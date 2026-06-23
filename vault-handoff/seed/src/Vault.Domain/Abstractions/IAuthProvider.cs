namespace Vault.Domain.Abstractions;

public record ExternalIdentity(string Email, string DisplayName);

/// <summary>Validates a sign-in (Google OAuth in prod) and returns the verified identity.</summary>
public interface IAuthProvider
{
    Task<ExternalIdentity> ValidateAsync(string code, CancellationToken ct);
}
