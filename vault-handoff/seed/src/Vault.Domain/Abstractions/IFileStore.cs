namespace Vault.Domain.Abstractions;

/// <summary>Binary storage port. Default impl = local NAS/filesystem; swappable for S3.</summary>
public interface IFileStore
{
    /// <summary>Save content; returns the opaque storage key to persist on the file row.</summary>
    Task<string> SaveAsync(Stream content, string suggestedName, CancellationToken ct);

    Task<Stream> OpenAsync(string key, CancellationToken ct);

    /// <summary>Copy an existing object to a new key (used to snapshot revision backups).</summary>
    Task<string> CopyAsync(string sourceKey, string destSuggestedName, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);
}
