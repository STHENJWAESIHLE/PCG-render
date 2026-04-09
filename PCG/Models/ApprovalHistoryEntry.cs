using System.ComponentModel.DataAnnotations;

namespace PCG.Models;

public class ApprovalHistoryEntry
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public ApprovalStage Stage { get; set; }
    public ApprovalDecision Decision { get; set; }

    public string? ActorUserId { get; set; }
    public ApplicationUser? Actor { get; set; }

    public DateTime ActedAtUtc { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }
}
