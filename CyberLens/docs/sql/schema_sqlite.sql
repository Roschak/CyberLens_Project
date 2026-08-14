-- ==============================================================================
-- CyberLens OSINT Platform - Database Schema (SQLite)
-- Author: Gravicode Studios / Antigravity AI
-- ==============================================================================

CREATE TABLE IF NOT EXISTS "Users" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Username" TEXT NOT NULL UNIQUE,
    "PasswordHash" TEXT NOT NULL,
    "DisplayName" TEXT NOT NULL,
    "Role" INTEGER NOT NULL DEFAULT 2, -- 0: Admin, 1: Analyst, 2: Operator
    "Email" TEXT NULL,
    "AvatarColor" TEXT NULL DEFAULT '#FF4D00',
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "CreatedAt" TEXT NOT NULL,
    "LastLoginAt" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "Categories" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Color" TEXT NOT NULL DEFAULT '#FF4D00',
    "Description" TEXT NULL,
    "CreatedAt" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "Sources" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Kind" INTEGER NOT NULL DEFAULT 0, -- 0: Rss, 1: Reddit, 2: Mastodon, 3: YouTube, 4: Twitter, 5: Facebook, 6: Threads, 7: TikTok, 8: DarkWeb, 9: News, 10: Forum, 11: Custom
    "Url" TEXT NOT NULL,
    "CategoryId" INTEGER NULL REFERENCES "Categories"("Id") ON DELETE SET NULL,
    "Country" TEXT NOT NULL DEFAULT 'Global',
    "TrustScore" REAL NOT NULL DEFAULT 0.8,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "PollIntervalMinutes" INTEGER NOT NULL DEFAULT 15,
    "LastPolledAt" TEXT NULL,
    "StatusMessage" TEXT NULL,
    "CreatedAt" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "Posts" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "SourceId" INTEGER NOT NULL REFERENCES "Sources"("Id") ON DELETE RESTRICT,
    "CategoryId" INTEGER NULL REFERENCES "Categories"("Id") ON DELETE SET NULL,
    "Title" TEXT NULL,
    "Content" TEXT NOT NULL,
    "Author" TEXT NULL,
    "AuthorHandle" TEXT NULL,
    "Url" TEXT NULL,
    "PublishedAt" TEXT NOT NULL,
    "CollectedAt" TEXT NOT NULL,
    "SentimentScore" REAL NOT NULL DEFAULT 0.0,
    "SentimentLabel" TEXT NOT NULL DEFAULT 'neutral',
    "Likes" INTEGER NOT NULL DEFAULT 0,
    "Shares" INTEGER NOT NULL DEFAULT 0,
    "Comments" INTEGER NOT NULL DEFAULT 0,
    "Latitude" REAL NULL,
    "Longitude" REAL NULL,
    "LocationName" TEXT NULL,
    "Language" TEXT NOT NULL DEFAULT 'id',
    "Tags" TEXT NULL,
    "Hash" TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_Posts_PublishedAt" ON "Posts"("PublishedAt");
CREATE INDEX IF NOT EXISTS "IX_Posts_Hash" ON "Posts"("Hash");

CREATE TABLE IF NOT EXISTS "PostMedia" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "PostId" INTEGER NOT NULL REFERENCES "Posts"("Id") ON DELETE CASCADE,
    "Kind" INTEGER NOT NULL DEFAULT 0, -- 0: Image, 1: Video, 2: Audio
    "Url" TEXT NOT NULL,
    "Caption" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "WatchKeywords" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Term" TEXT NOT NULL,
    "IsRegex" INTEGER NOT NULL DEFAULT 0,
    "CategoryId" INTEGER NULL REFERENCES "Categories"("Id") ON DELETE SET NULL,
    "NotifyEmail" TEXT NULL,
    "MinSentiment" REAL NOT NULL DEFAULT -1.0,
    "Severity" INTEGER NOT NULL DEFAULT 1, -- 0: Info, 1: Warning, 2: Critical
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "HitCount" INTEGER NOT NULL DEFAULT 0,
    "CreatedAt" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "Alerts" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "KeywordId" INTEGER NULL REFERENCES "WatchKeywords"("Id") ON DELETE SET NULL,
    "PostId" INTEGER NULL REFERENCES "Posts"("Id") ON DELETE SET NULL,
    "Title" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "Severity" INTEGER NOT NULL DEFAULT 1,
    "CreatedAt" TEXT NOT NULL,
    "IsRead" INTEGER NOT NULL DEFAULT 0,
    "ReadAt" TEXT NULL
);

CREATE INDEX IF NOT EXISTS "IX_Alerts_CreatedAt" ON "Alerts"("CreatedAt");

CREATE TABLE IF NOT EXISTS "EntityNodes" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Kind" INTEGER NOT NULL DEFAULT 5, -- 0: Person, 1: Organization, 2: Location, 3: Event, 4: Product, 5: Topic, 6: ThreatGroup, 7: Infrastructure
    "Mentions" INTEGER NOT NULL DEFAULT 1,
    "RiskScore" REAL NOT NULL DEFAULT 0.0,
    "ExtraData" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "EntityLinks" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "SourceNodeId" INTEGER NOT NULL REFERENCES "EntityNodes"("Id") ON DELETE CASCADE,
    "TargetNodeId" INTEGER NOT NULL REFERENCES "EntityNodes"("Id") ON DELETE CASCADE,
    "RelationType" TEXT NOT NULL DEFAULT 'related_to',
    "Weight" REAL NOT NULL DEFAULT 1.0,
    "FirstSeenAt" TEXT NOT NULL,
    "LastSeenAt" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "ChatSessions" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Title" TEXT NOT NULL DEFAULT 'Percakapan baru',
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "ChatMessages" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "SessionId" INTEGER NOT NULL REFERENCES "ChatSessions"("Id") ON DELETE CASCADE,
    "Role" INTEGER NOT NULL DEFAULT 0, -- 0: User, 1: Assistant, 2: System
    "Content" TEXT NOT NULL,
    "Model" TEXT NULL,
    "CreatedAt" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "ChatAttachments" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "MessageId" INTEGER NOT NULL REFERENCES "ChatMessages"("Id") ON DELETE CASCADE,
    "FileName" TEXT NOT NULL,
    "Url" TEXT NOT NULL,
    "ContentType" TEXT NOT NULL,
    "IsImage" INTEGER NOT NULL DEFAULT 0,
    "SizeBytes" INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS "AuditLogs" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "UserId" INTEGER NULL REFERENCES "Users"("Id") ON DELETE SET NULL,
    "Username" TEXT NOT NULL,
    "Action" TEXT NOT NULL,
    "Detail" TEXT NOT NULL,
    "IpAddress" TEXT NULL,
    "Timestamp" TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_AuditLogs_Timestamp" ON "AuditLogs"("Timestamp");

CREATE TABLE IF NOT EXISTS "Reports" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Title" TEXT NOT NULL,
    "Kind" INTEGER NOT NULL DEFAULT 0, -- 0: Daily, 1: Weekly, 2: Monthly, 3: Custom
    "Format" INTEGER NOT NULL DEFAULT 0, -- 0: Pdf, 1: Excel, 2: Json
    "StoragePath" TEXT NOT NULL,
    "CreatedById" INTEGER NULL REFERENCES "Users"("Id") ON DELETE SET NULL,
    "PeriodStart" TEXT NOT NULL,
    "PeriodEnd" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "CrawlRuns" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Connector" TEXT NOT NULL,
    "Kind" INTEGER NOT NULL DEFAULT 0,
    "Trigger" TEXT NOT NULL DEFAULT 'Scheduled',
    "StartedAt" TEXT NOT NULL,
    "FinishedAt" TEXT NOT NULL,
    "DurationMs" INTEGER NOT NULL DEFAULT 0,
    "ItemsFound" INTEGER NOT NULL DEFAULT 0,
    "ItemsAdded" INTEGER NOT NULL DEFAULT 0,
    "ItemsDuplicate" INTEGER NOT NULL DEFAULT 0,
    "Success" INTEGER NOT NULL DEFAULT 1,
    "Error" TEXT NULL
);
