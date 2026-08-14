using CyberLens.Data;
using CyberLens.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace CyberLens.Services.Chat;

/// <summary>
/// Orchestrates Bang Kevin chat: manages multi-session persistence, builds the prompt
/// (system persona + history + attachments) and calls the configured LLM with tools enabled.
/// </summary>
public class ChatService(
    IDbContextFactory<CyberLensDbContext> dbFactory,
    AiKernelFactory kernelFactory,
    AppSettingsService settings)
{
    public async Task<List<ChatSession>> GetSessionsAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ChatSessions.Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt).ToListAsync();
    }

    public async Task<ChatSession> CreateSessionAsync(int userId, string title = "Percakapan baru")
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var session = new ChatSession { UserId = userId, Title = title };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public async Task DeleteSessionAsync(int sessionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.ChatSessions.FindAsync(sessionId);
        if (s is not null) { db.ChatSessions.Remove(s); await db.SaveChangesAsync(); }
    }

    public async Task ResetSessionAsync(int sessionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var msgs = db.ChatMessages.Where(m => m.SessionId == sessionId);
        db.ChatMessages.RemoveRange(msgs);
        var s = await db.ChatSessions.FindAsync(sessionId);
        if (s is not null) { s.Title = "Percakapan baru"; s.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(int sessionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ChatMessages.Include(m => m.Attachments)
            .Where(m => m.SessionId == sessionId).OrderBy(m => m.CreatedAt).ToListAsync();
    }

    public record PendingAttachment(string FileName, string Url, string ContentType, bool IsImage, long Size);

    /// <summary>Persist the user's message, run the LLM, persist and return the assistant reply.</summary>
    public async Task<ChatMessage> SendAsync(int sessionId, string userText, List<PendingAttachment>? attachments = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var session = await db.ChatSessions.FindAsync(sessionId)
            ?? throw new InvalidOperationException("Sesi tidak ditemukan.");

        var userMsg = new ChatMessage { SessionId = sessionId, Role = ChatRole.User, Content = userText };
        foreach (var a in attachments ?? new())
            userMsg.Attachments.Add(new ChatAttachment
            {
                FileName = a.FileName, Url = a.Url, ContentType = a.ContentType,
                IsImage = a.IsImage, SizeBytes = a.Size
            });
        db.ChatMessages.Add(userMsg);

        // First user message becomes the session title.
        if (session.Title == "Percakapan baru" && !string.IsNullOrWhiteSpace(userText))
            session.Title = userText.Length > 60 ? userText[..57] + "..." : userText;
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        string replyText;
        var cfg = settings.Current.Ai;
        var model = kernelFactory.ActiveModel;
        try
        {
            var kernel = kernelFactory.Build();
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var history = await BuildHistoryAsync(db, sessionId, cfg.SystemPrompt);

            var execSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = cfg.Temperature,
                MaxTokens = cfg.MaxTokens,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };
            var result = await chat.GetChatMessageContentsAsync(history, execSettings, kernel);
            replyText = result.FirstOrDefault()?.Content ?? "(tidak ada balasan)";
        }
        catch (Exception ex)
        {
            replyText = $"⚠️ Maaf, terjadi kendala saat memanggil model **{model}** ({cfg.Provider}).\n\n" +
                        $"Detail: `{ex.Message}`\n\nPastikan API key & konfigurasi provider sudah benar di halaman **Settings**.";
        }

        var assistantMsg = new ChatMessage
        {
            SessionId = sessionId, Role = ChatRole.Assistant, Content = replyText, Model = model
        };
        db.ChatMessages.Add(assistantMsg);
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return assistantMsg;
    }

    private static async Task<ChatHistory> BuildHistoryAsync(CyberLensDbContext db, int sessionId, string systemPrompt)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);

        var msgs = await db.ChatMessages.Include(m => m.Attachments)
            .Where(m => m.SessionId == sessionId).OrderBy(m => m.CreatedAt).ToListAsync();

        foreach (var m in msgs)
        {
            if (m.Role == ChatRole.User)
            {
                var items = new ChatMessageContentItemCollection();
                var text = m.Content;
                // Documents are referenced by link inside the text; images become image content.
                foreach (var a in m.Attachments.Where(a => !a.IsImage))
                    text += $"\n[Lampiran dokumen: {a.FileName}]({AbsoluteUrl(a.Url)})";
                items.Add(new TextContent(text));
                foreach (var a in m.Attachments.Where(a => a.IsImage))
                    items.Add(new ImageContent(new Uri(AbsoluteUrl(a.Url))));
                history.AddUserMessage(items);
            }
            else if (m.Role == ChatRole.Assistant)
            {
                history.AddAssistantMessage(m.Content);
            }
        }
        return history;
    }

    // Attachments are stored as /files/... relative URLs; the model needs absolute for image fetch.
    private static string _baseUrl = "http://localhost";
    public static void SetBaseUrl(string url) => _baseUrl = url.TrimEnd('/');
    private static string AbsoluteUrl(string url) =>
        url.StartsWith("http") ? url : $"{_baseUrl}{url}";
}
