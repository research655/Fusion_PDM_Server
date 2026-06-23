namespace Vault.Domain.Files;

public enum FileState
{
    Initial,
    AwaitingApproval,
    Production,
    UnderChange,
    InitialRejected,
    UnderChangeRejected,
    Obsolete,
    Prototype
}

public enum FileTrigger { Create, Submit, Approve, Reject, ReturnToEditable, BeginChange, MarkObsolete, EnterPrototype, ExitPrototype }

/// <summary>Which track a file is on while in AwaitingApproval, so a reject routes correctly.</summary>
public enum ApprovalOrigin { InitialTrack, ChangeTrack }

/// <summary>Authoritative state machine. Any transition not defined here throws.</summary>
public static class FileStateMachine
{
    public static ApprovalOrigin OriginFor(FileState submittingFrom) => submittingFrom switch
    {
        FileState.Initial or FileState.InitialRejected => ApprovalOrigin.InitialTrack,
        FileState.UnderChange or FileState.UnderChangeRejected => ApprovalOrigin.ChangeTrack,
        _ => throw new InvalidOperationException($"Cannot submit from state {submittingFrom}.")
    };

    public static FileState Next(FileState current, FileTrigger trigger, ApprovalOrigin? origin = null)
    {
        switch (trigger)
        {
            case FileTrigger.Submit
                when current is FileState.Initial or FileState.InitialRejected
                                or FileState.UnderChange or FileState.UnderChangeRejected:
                return FileState.AwaitingApproval;

            case FileTrigger.Approve when current is FileState.AwaitingApproval:
                return FileState.Production;

            case FileTrigger.Reject when current is FileState.AwaitingApproval:
                return (origin ?? throw new InvalidOperationException("Reject requires the approval origin."))
                    switch
                    {
                        ApprovalOrigin.InitialTrack => FileState.InitialRejected,
                        ApprovalOrigin.ChangeTrack => FileState.UnderChangeRejected,
                        _ => throw new InvalidOperationException("Unknown approval origin.")
                    };

            // "Back to Initial / Back to Under Change": undo a pending submission (from
            // AwaitingApproval, by track) OR return a rejected file to its editable state.
            // Always lands in the pre-submission editable state. No approver notification (service).
            case FileTrigger.ReturnToEditable when current is FileState.AwaitingApproval:
                return (origin ?? throw new InvalidOperationException("Return requires the approval origin."))
                    switch
                    {
                        ApprovalOrigin.InitialTrack => FileState.Initial,
                        ApprovalOrigin.ChangeTrack => FileState.UnderChange,
                        _ => throw new InvalidOperationException("Unknown approval origin.")
                    };

            case FileTrigger.ReturnToEditable when current is FileState.InitialRejected:
                return FileState.Initial;

            case FileTrigger.ReturnToEditable when current is FileState.UnderChangeRejected:
                return FileState.UnderChange;

            case FileTrigger.BeginChange when current is FileState.Production:
                return FileState.UnderChange;

            // Obsolete may be entered from Production OR Prototype. The prior state is recorded
            // (VaultFile.ObsoletedFromState) so Recover can restore it exactly.
            case FileTrigger.MarkObsolete when current is FileState.Production or FileState.Prototype:
                return FileState.Obsolete;

            // Reactivate/Recover target is data-driven (the recorded prior state), so the service
            // sets it directly after validating it is Production or Prototype — not routed here.

            // Prototype: a never-approved file (Initial) may be flipped to Prototype and back,
            // by Engineers/Admins, with no approval. (No revision / no history handled in the service.)
            case FileTrigger.EnterPrototype when current is FileState.Initial:
                return FileState.Prototype;

            case FileTrigger.ExitPrototype when current is FileState.Prototype:
                return FileState.Initial;

            default:
                throw new InvalidOperationException($"Invalid transition: {trigger} from {current}.");
        }
    }

    /// <summary>
    /// A file may be checked out only while in a clean editable state: Initial or Under Change.
    /// A rejected file is NOT checkout-able — return it to Initial/Under Change first.
    /// </summary>
    public static bool IsCheckOutable(FileState state) => state is
        FileState.Initial or FileState.UnderChange;
}
