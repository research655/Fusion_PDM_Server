using Vault.Domain.Abstractions;

namespace Vault.Infrastructure.Auth;

/// <summary>Dev stub: returns a fixed identity so the app runs without Google.
/// TODO(Cursor): real Google OAuth code exchange; the @sparkrobotic.com check lives in VaultService.</summary>
public sealed class StubAuthProvider : IAuthProvider
{
    public Task<ExternalIdentity> ValidateAsync(string code, CancellationToken ct)
        => Task.FromResult(new ExternalIdentity("dev@sparkrobotic.com", "Dev User"));
}
