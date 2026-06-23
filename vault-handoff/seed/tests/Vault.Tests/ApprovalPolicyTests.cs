using Vault.Domain.Approvals;
using Vault.Domain.Entities;
using Xunit;

namespace Vault.Tests;

public class ApprovalPolicyTests
{
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    [Fact]
    public void User_cannot_approve()
        => Assert.False(ApprovalPolicy.CanApprove(UserRole.User, Alice, Bob));

    [Fact]
    public void Engineer_cannot_approve_own_work()
        => Assert.False(ApprovalPolicy.CanApprove(UserRole.Engineer, Alice, Alice));

    [Fact]
    public void Engineer_can_approve_others_work()
        => Assert.True(ApprovalPolicy.CanApprove(UserRole.Engineer, Alice, Bob));

    [Fact]
    public void Admin_can_approve_own_work()
        => Assert.True(ApprovalPolicy.CanApprove(UserRole.Admin, Alice, Alice));

    [Fact]
    public void Admin_can_approve_others_work()
        => Assert.True(ApprovalPolicy.CanApprove(UserRole.Admin, Alice, Bob));
}
