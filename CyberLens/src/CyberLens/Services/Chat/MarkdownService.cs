using Markdig;
using Microsoft.AspNetCore.Components;

namespace CyberLens.Services.Chat;

/// <summary>Renders Markdown to HTML for chat threads: tables, media, code, links (GFM + soft-line-breaks).</summary>
public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .UseEmojiAndSmiley()
        .UseAutoLinks()
        .Build();

    public MarkupString ToHtml(string? markdown)
        => new(string.IsNullOrEmpty(markdown) ? "" : Markdown.ToHtml(markdown, _pipeline));
}
