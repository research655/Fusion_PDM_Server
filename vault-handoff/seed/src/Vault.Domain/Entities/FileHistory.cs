namespace Vault.Domain.Entities;

/// <summary>Append-only audit log. One row per state/revision/check/rollback event.</summary>
public class FileHistory
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public string Event { get; set; } = "";   // CheckedOut, CheckedIn, Submitted, Approved, Rejected, Revised, RolledBack
    public string? FromState { get; set; }
    public string? ToState { get; set; }
    public string? Revision { get; set; }      // revision after the event, if it changed
    public Guid Actor { get; set; }
    public DateTimeOffset At { get; set; }
    public string? Note { get; set; }          // e.g. "Rolled back to B by Jane Admin"
}
