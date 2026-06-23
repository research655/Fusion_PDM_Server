using Vault.Domain.Files;

namespace Vault.Domain.Entities;

public class VaultFile
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }            // which vault this file lives in (default: CAD)
    public Repository? Repository { get; set; }       // nav; loaded on reads so the card can expose the key
    public string Number { get; set; } = "";          // user-entered, validated charset
    public string Name { get; set; } = "";            // filename as uploaded (with extension)
    public string NameKey { get; set; } = "";         // FileNaming.ToKey(Name); UNIQUE per RepositoryId
    public string Description { get; set; } = "";
    public string? Revision { get; set; }             // null until first approval
    public FileState State { get; set; }
    public FileState? ObsoletedFromState { get; set; } // Production or Prototype, recorded on obsolete; used by Recover
    public ApprovalOrigin? Origin { get; set; }       // set while AwaitingApproval; drives reject routing
    public Guid? CheckedOutBy { get; set; }           // null = checked in / read-only
    public Guid? SubmittedBy { get; set; }            // who submitted for approval (self-approval check)
    public Guid DesignedBy { get; set; }
    public DateTimeOffset DesignedDate { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedDate { get; set; }
    public Guid? ChangedBy { get; set; }
    public DateTimeOffset? ChangedDate { get; set; }
    public string StorageKey { get; set; } = "";      // live binary pointer in IFileStore
    public int ContentVersion { get; set; }            // bumped on every content change; for client cache invalidation
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
