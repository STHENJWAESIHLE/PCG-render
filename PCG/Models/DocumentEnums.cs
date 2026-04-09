namespace PCG.Models;

public enum DocumentType
{
    Invoice = 0,
    CreditNote = 1
}

/// <summary>Tracks which approval gate the document is waiting on, or terminal state.</summary>
public enum DocumentStatus
{
    PendingReviewer = 0,
    PendingManager = 1,
    PendingAdmin = 2,
    Approved = 3,
    Rejected = 4
}

public enum DuplicateFlag
{
    None = 0,
    InvoiceNumberMatch = 1,
    VendorAmountSuspected = 2,
    Both = 3,
    IdenticalFile = 4
}

public enum ApprovalStage
{
    Reviewer = 1,
    Manager = 2,
    Admin = 3
}

public enum ApprovalDecision
{
    Approved = 0,
    Rejected = 1
}
