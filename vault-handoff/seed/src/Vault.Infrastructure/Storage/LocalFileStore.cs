using Microsoft.Extensions.Options;
using Vault.Domain.Abstractions;

namespace Vault.Infrastructure.Storage;

public sealed class FileStoreOptions
{
    public string RootPath { get; set; } = "VaultData";
}

/// <summary>Local NAS/filesystem store. Keys are paths relative to RootPath.</summary>
public sealed class LocalFileStore : IFileStore
{
    private readonly string _root;

    public LocalFileStore(IOptions<FileStoreOptions> options) => _root = options.Value.RootPath;

    public async Task<string> SaveAsync(Stream content, string suggestedName, CancellationToken ct)
    {
        var key = $"{Guid.NewGuid():N}{Path.GetExtension(suggestedName)}";
        var full = FullPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        return key;
    }

    public Task<Stream> OpenAsync(string key, CancellationToken ct)
        => Task.FromResult<Stream>(File.OpenRead(FullPath(key)));

    public async Task<string> CopyAsync(string sourceKey, string destSuggestedName, CancellationToken ct)
    {
        var key = $"{destSuggestedName}{Path.GetExtension(sourceKey)}";
        var full = FullPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var src = File.OpenRead(FullPath(sourceKey));
        await using var dst = File.Create(full);
        await src.CopyToAsync(dst, ct);
        return key;
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var full = FullPath(key);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    private string FullPath(string key) => Path.Combine(_root, key);
}
