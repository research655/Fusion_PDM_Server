namespace Vault.Domain.Entities;

/// <summary>
/// Hidden snapshot of a Production revision, taken on every approval into Production.
/// NEVER returned by /search or GET /files/{id}. An Admin enumerates these only via
/// the Admin-only revisions endpoint to choose a rollback target.
/// </summary>
public class FileRevisionBackup
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public string Revision { get; set; } = "";
    public string Number { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedDate { get; set; }
    public string StorageKey { get; set; } = "";   // archived binary, separate from the live file
    public DateTimeOffset CreatedAt { get; set; }
}
