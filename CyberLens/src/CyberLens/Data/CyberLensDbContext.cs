using Microsoft.EntityFrameworkCore;

namespace CyberLens.Data;

public class CyberLensDbContext : DbContext
{
    public CyberLensDbContext(DbContextOptions<CyberLensDbContext> options) : base(options) { }

    public DbSet<AppUser> Users { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Source> Sources { get; set; } = null!;
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<PostMedia> PostMedia { get; set; } = null!;
    public DbSet<WatchKeyword> WatchKeywords { get; set; } = null!;
    public DbSet<Alert> Alerts { get; set; } = null!;
    public DbSet<EntityNode> EntityNodes { get; set; } = null!;
    public DbSet<EntityLink> EntityLinks { get; set; } = null!;
    public DbSet<ChatSession> ChatSessions { get; set; } = null!;
    public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
    public DbSet<ChatAttachment> ChatAttachments { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<ReportRecord> Reports { get; set; } = null!;
    public DbSet<CrawlRun> CrawlRuns { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // AppUser
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // Post
        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasIndex(e => e.PublishedAt);
            entity.HasIndex(e => e.Hash);

            entity.HasOne(p => p.Source)
                .WithMany(s => s.Posts)
                .HasForeignKey(p => p.SourceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Category)
                .WithMany(c => c.Posts)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // PostMedia
        modelBuilder.Entity<PostMedia>(entity =>
        {
            entity.HasOne(m => m.Post)
                .WithMany(p => p.Media)
                .HasForeignKey(m => m.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // WatchKeyword
        modelBuilder.Entity<WatchKeyword>(entity =>
        {
            entity.HasOne(w => w.Category)
                .WithMany()
                .HasForeignKey(w => w.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Alert
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(a => a.Keyword)
                .WithMany(k => k.Alerts)
                .HasForeignKey(a => a.KeywordId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.Post)
                .WithMany(p => p.Alerts)
                .HasForeignKey(a => a.PostId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // EntityLink
        modelBuilder.Entity<EntityLink>(entity =>
        {
            entity.HasOne(l => l.SourceNode)
                .WithMany(n => n.OutgoingLinks)
                .HasForeignKey(l => l.SourceNodeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.TargetNode)
                .WithMany(n => n.IncomingLinks)
                .HasForeignKey(l => l.TargetNodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatSession
        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasOne(s => s.User)
                .WithMany(u => u.ChatSessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatMessage
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasOne(m => m.Session)
                .WithMany(s => s.Messages)
                .HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatAttachment
        modelBuilder.Entity<ChatAttachment>(entity =>
        {
            entity.HasOne(a => a.Message)
                .WithMany(m => m.Attachments)
                .HasForeignKey(a => a.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => e.Timestamp);

            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ReportRecord
        modelBuilder.Entity<ReportRecord>(entity =>
        {
            entity.HasOne(r => r.CreatedBy)
                .WithMany(u => u.Reports)
                .HasForeignKey(r => r.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
