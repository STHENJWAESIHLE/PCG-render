using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PCG.Models;

namespace PCG.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ApprovalHistoryEntry> ApprovalHistoryEntries => Set<ApprovalHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Document>(e =>
        {
            e.HasIndex(d => d.InvoiceNumber);
            e.HasIndex(d => d.Vendor);
            e.HasIndex(d => d.Status);
            e.HasIndex(d => d.UploadedAtUtc);
            e.HasOne(d => d.UploadedBy).WithMany().HasForeignKey(d => d.UploadedById).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(d => d.DuplicateOfDocument).WithMany().HasForeignKey(d => d.DuplicateOfDocumentId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<ApprovalHistoryEntry>(e =>
        {
            e.HasOne(h => h.Document).WithMany(d => d.ApprovalHistory).HasForeignKey(h => h.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.Actor).WithMany().HasForeignKey(h => h.ActorUserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
