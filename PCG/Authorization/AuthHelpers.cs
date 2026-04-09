using System.Security.Claims;
using PCG.Models;

namespace PCG.Authorization;

public static class AuthHelpers
{
    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(RoleNames.Admin);

    public static bool IsExternalUser(this ClaimsPrincipal user) => user.IsInRole(RoleNames.ExternalUser);

    public static bool CanUpload(this ClaimsPrincipal user) =>
        user.IsInRole(RoleNames.Admin) ||
        user.IsInRole(RoleNames.ExternalUser);

    /// <summary>External users can upload but cannot approve.</summary>
    public static bool CanApprove(this ClaimsPrincipal user) =>
        !user.IsInRole(RoleNames.ExternalUser) &&
        (user.IsInRole(RoleNames.Admin) ||
         user.IsInRole(RoleNames.Reviewer) ||
         user.IsInRole(RoleNames.Manager));

    public static ApprovalStage? ExpectedStage(DocumentStatus status) => status switch
    {
        DocumentStatus.PendingReviewer => ApprovalStage.Reviewer,
        DocumentStatus.PendingManager => ApprovalStage.Manager,
        DocumentStatus.PendingAdmin => ApprovalStage.Admin,
        _ => null
    };

    public static bool CanActAtStage(this ClaimsPrincipal user, ApprovalStage stage)
    {
        // External users cannot approve anything
        if (user.IsInRole(RoleNames.ExternalUser)) return false;

        if (user.IsInRole(RoleNames.Admin)) return true;
        return stage switch
        {
            ApprovalStage.Reviewer => user.IsInRole(RoleNames.Reviewer),
            ApprovalStage.Manager => user.IsInRole(RoleNames.Manager),
            ApprovalStage.Admin => user.IsInRole(RoleNames.Admin),
            _ => false
        };
    }

    public static DocumentStatus NextStatusAfterApproval(DocumentStatus current) => current switch
    {
        DocumentStatus.PendingReviewer => DocumentStatus.PendingManager,
        DocumentStatus.PendingManager => DocumentStatus.PendingAdmin,
        DocumentStatus.PendingAdmin => DocumentStatus.Approved,
        _ => current
    };
}
