using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CyberLens.Data;

// ---------- Enums ----------

public enum UserRole
{
    Admin,
    Analyst,
    Operator,
    Viewer
}

public enum SourceKind
{
    Rss,
    Reddit,
    Mastodon,
    YouTube,
    Twitter,
    Facebook,
    Threads,
    TikTok,
    DarkWeb,
    News,
    Forum,
    SocialMedia,
    Blog,
    Official,
    Custom
}

public enum MediaType
{
    Image,
    Video,
    Audio
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

public enum EntityKind
{
    Person,
    Organization,
    Location,
    Event,
    Product,
    Topic,
    ThreatGroup,
    Infrastructure
}

public enum ChatRole
{
    User,
    Assistant,
    System
}

public enum ReportKind
{
    Daily,
    Weekly,
    Monthly,
    Custom
}

public enum ReportFormat
{
    Pdf,
    Excel,
    Json
}

// ---------- Entities ----------

public class AppUser
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Operator;

    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20)]
    public string AvatarColor { get; set; } = "#FF4D00";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();

    public ICollection<ReportRecord> Reports { get; set; } = new List<ReportRecord>();
}

public class Category
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Color { get; set; } = "#FF4D00";

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Post> Posts { get; set; } = new List<Post>();

    public ICollection<Source> Sources { get; set; } = new List<Source>();
}

public class Source
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public SourceKind Kind { get; set; } = SourceKind.News;

    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    public Category? Category { get; set; }

    [MaxLength(50)]
    public string Country { get; set; } = "Global";

    public double TrustScore { get; set; } = 0.8;

    public bool IsActive { get; set; } = true;

    public int PollIntervalMinutes { get; set; } = 15;

    public DateTime? LastPolledAt { get; set; }

    [MaxLength(500)]
    public string? StatusMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Post> Posts { get; set; } = new List<Post>();
}

public class Post
{
    public int Id { get; set; }

    public int SourceId { get; set; }

    public Source? Source { get; set; }

    public int? CategoryId { get; set; }

    public Category? Category { get; set; }

    [MaxLength(250)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Author { get; set; } = string.Empty;

    [MaxLength(100)]
    public string AuthorHandle { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

    public double SentimentScore { get; set; } = 0.0;

    [MaxLength(20)]
    public string SentimentLabel { get; set; } = "neutral";

    public int Likes { get; set; } = 0;

    public int Shares { get; set; } = 0;

    public int Comments { get; set; } = 0;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    [MaxLength(150)]
    public string? LocationName { get; set; }

    [MaxLength(10)]
    public string Language { get; set; } = "id";

    public string Tags { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Hash { get; set; } = string.Empty;

    public ICollection<PostMedia> Media { get; set; } = new List<PostMedia>();

    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}

public class PostMedia
{
    public int Id { get; set; }

    public int PostId { get; set; }

    public Post? Post { get; set; }

    public MediaType Kind { get; set; } = MediaType.Image;

    [Required]
    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(250)]
    public string Caption { get; set; } = string.Empty;
}

public class WatchKeyword
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Term { get; set; } = string.Empty;

    public bool IsRegex { get; set; } = false;

    public int? CategoryId { get; set; }

    public Category? Category { get; set; }

    [MaxLength(150)]
    public string NotifyEmail { get; set; } = string.Empty;

    public double MinSentiment { get; set; } = -1.0;

    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;

    public bool IsActive { get; set; } = true;

    public bool NotifyRealtime { get; set; } = true;

    public int HitCount { get; set; } = 0;

    public int? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}

public class Alert
{
    public int Id { get; set; }

    public int? KeywordId { get; set; }

    public WatchKeyword? Keyword { get; set; }

    public int? PostId { get; set; }

    public Post? Post { get; set; }

    [Required]
    [MaxLength(250)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;

    public DateTime? ReadAt { get; set; }
}

public class EntityNode
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public EntityKind Kind { get; set; } = EntityKind.Topic;

    public int Mentions { get; set; } = 1;

    public double RiskScore { get; set; } = 0.0;

    public string? ExtraData { get; set; }

    public ICollection<EntityLink> OutgoingLinks { get; set; } = new List<EntityLink>();

    public ICollection<EntityLink> IncomingLinks { get; set; } = new List<EntityLink>();
}

public class EntityLink
{
    public int Id { get; set; }

    public int SourceNodeId { get; set; }

    public EntityNode? SourceNode { get; set; }

    public int TargetNodeId { get; set; }

    public EntityNode? TargetNode { get; set; }

    [Required]
    [MaxLength(100)]
    public string RelationType { get; set; } = "related_to";

    public double Weight { get; set; } = 1.0;

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}

public class ChatSession
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public AppUser? User { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "Percakapan baru";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ChatMessage
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public ChatSession? Session { get; set; }

    public ChatRole Role { get; set; } = ChatRole.User;

    [Required]
    public string Content { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Model { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatAttachment> Attachments { get; set; } = new List<ChatAttachment>();
}

public class ChatAttachment
{
    public int Id { get; set; }

    public int MessageId { get; set; }

    public ChatMessage? Message { get; set; }

    [Required]
    [MaxLength(250)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = "application/octet-stream";

    public bool IsImage { get; set; } = false;

    public long SizeBytes { get; set; } = 0L;
}

public class AuditLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public AppUser? User { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    public string Detail { get; set; } = string.Empty;

    [MaxLength(50)]
    public string IpAddress { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ReportRecord
{
    public int Id { get; set; }

    [Required]
    [MaxLength(250)]
    public string Title { get; set; } = string.Empty;

    public ReportKind Kind { get; set; } = ReportKind.Daily;

    public ReportFormat Format { get; set; } = ReportFormat.Pdf;

    [Required]
    [MaxLength(500)]
    public string StoragePath { get; set; } = string.Empty;

    public int? CreatedById { get; set; }

    public AppUser? CreatedBy { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CrawlRun
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Connector { get; set; } = string.Empty;

    public SourceKind Kind { get; set; } = SourceKind.News;

    [Required]
    [MaxLength(50)]
    public string Trigger { get; set; } = "Scheduled";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime FinishedAt { get; set; } = DateTime.UtcNow;

    public int DurationMs { get; set; } = 0;

    public int ItemsFound { get; set; } = 0;

    public int ItemsAdded { get; set; } = 0;

    public int ItemsDuplicate { get; set; } = 0;

    public bool Success { get; set; } = true;

    [MaxLength(1000)]
    public string? Error { get; set; }
}
