using Vault.Domain.Entities;
using Vault.Domain.Files;
using Xunit;

namespace Vault.Tests;

public class FileStateTests
{
    [Theory]
    [InlineData(FileState.Initial, true)]
    [InlineData(FileState.UnderChange, true)]
    [InlineData(FileState.InitialRejected, false)]
    [InlineData(FileState.UnderChangeRejected, false)]
    [InlineData(FileState.AwaitingApproval, false)]
    [InlineData(FileState.Production, false)]
    [InlineData(FileState.Obsolete, false)]
    [InlineData(FileState.Prototype, false)]
    public void Only_clean_editable_states_are_checkoutable(FileState s, bool expected)
        => Assert.Equal(expected, FileStateMachine.IsCheckOutable(s));

    [Theory]
    [InlineData(FileState.InitialRejected, FileState.Initial)]
    [InlineData(FileState.UnderChangeRejected, FileState.UnderChange)]
    public void ReturnToEditable_moves_a_rejected_file_back_to_editable(FileState from, FileState to)
        => Assert.Equal(to, FileStateMachine.Next(from, FileTrigger.ReturnToEditable));

    [Fact]
    public void Production_can_begin_change()
        => Assert.Equal(FileState.UnderChange,
            FileStateMachine.Next(FileState.Production, FileTrigger.BeginChange));

    [Theory]
    [InlineData(FileState.Production)]
    [InlineData(FileState.Prototype)]
    public void Production_or_prototype_can_be_obsoleted(FileState s)
        => Assert.Equal(FileState.Obsolete, FileStateMachine.Next(s, FileTrigger.MarkObsolete));

    [Theory]
    [InlineData(ApprovalOrigin.InitialTrack, FileState.Initial)]
    [InlineData(ApprovalOrigin.ChangeTrack, FileState.UnderChange)]
    public void ReturnToEditable_undoes_a_pending_submission(ApprovalOrigin origin, FileState to)
        => Assert.Equal(to, FileStateMachine.Next(FileState.AwaitingApproval, FileTrigger.ReturnToEditable, origin));

    [Theory]
    [InlineData(FileState.Initial)]
    [InlineData(FileState.UnderChange)]
    [InlineData(FileState.Obsolete)]
    public void Cannot_obsolete_a_non_production_file(FileState s)
        => Assert.Throws<InvalidOperationException>(
            () => FileStateMachine.Next(s, FileTrigger.MarkObsolete));

    [Theory]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.Engineer, true)]
    [InlineData(UserRole.User, false)]
    public void Only_admin_or_engineer_changes_production(UserRole role, bool expected)
        => Assert.Equal(expected, ProductionPolicy.CanChangeProductionState(role));
}
