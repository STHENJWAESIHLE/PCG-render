namespace PCG.Authorization;

/// <summary>
/// RBAC: Admin (full + final approval), Approver pipeline (Reviewer → Manager → Admin), ExternalUser (upload only), Viewer (read-only).
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Reviewer = "Reviewer";
    public const string Manager = "Manager";
    public const string ExternalUser = "ExternalUser";
    public const string Viewer = "Viewer";

    public static readonly string[] All = { Admin, Reviewer, Manager, ExternalUser, Viewer };

    /// <summary>Anyone who can act in the approval chain.</summary>
    public static readonly string[] ApproverRoles = { Admin, Reviewer, Manager };

    /// <summary>Anyone who can upload documents (internal staff + external customers).</summary>
    public static readonly string[] CanUploadRoles = { Admin, Reviewer, Manager, ExternalUser };
}
