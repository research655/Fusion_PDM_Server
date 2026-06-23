using Vault.Domain.Entities;

namespace Vault.Domain.Approvals;

/// <summary>
/// Segregation of duties:
///  - Users cannot approve.
///  - Engineers and Admins can approve OTHER people's work.
///  - Self-approval (approver == submitter) is allowed ONLY for Admins.
/// </summary>
public static class ApprovalPolicy
{
    public static bool CanApprove(UserRole approverRole, Guid approverId, Guid? submittedBy)
    {
        if (approverRole == UserRole.User)
            return false;

        var isSelf = submittedBy.HasValue && submittedBy.Value == approverId;
        if (isSelf)
            return approverRole == UserRole.Admin;

        return true; // Engineer or Admin approving someone else's submission
    }
}
