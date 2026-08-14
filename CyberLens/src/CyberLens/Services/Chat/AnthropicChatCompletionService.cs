using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace CyberLens.Services.Chat;

/// <summary>
/// Minimal Semantic Kernel <see cref="IChatCompletionService"/> for the Anthropic Messages API
/// (model default: claude-fable-5). Supports images and an automatic tool-use loop that invokes
/// the kernel functions registered by <see cref="AiKernelFactory"/>.
/// </summary>
public class AnthropicChatCompletionService(HttpClient http, string apiKey, string modelId) : IChatCompletionService
{
    private static readonly JsonSerializerOptions J = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        var settings = OpenAIPromptExecutionSettings.FromExecutionSettings(executionSettings);
        var autoInvoke = kernel is not null &&
            settings.FunctionChoiceBehavior is not null; // FunctionChoiceBehavior.Auto() registered
        var tools = autoInvoke ? BuildTools(kernel!) : null;

        // Extract system prompt + convert history into Anthropic message array.
        var system = new StringBuilder();
        var messages = new JsonArray();
        foreach (var msg in chatHistory)
        {
            if (msg.Role == AuthorRole.System) { system.AppendLine(msg.Content); continue; }
            messages.Add(ToAnthropicMessage(msg));
        }

        for (var iteration = 0; iteration < 8; iteration++)
        {
            var request = new JsonObject
            {
                ["model"] = modelId,
                ["max_tokens"] = settings.MaxTokens ?? 2048,
                ["messages"] = messages.DeepClone(),
            };
            if (system.Length > 0) request["system"] = system.ToString();
            // Fable 5 rejects temperature; only send for other models.
            if (settings.Temperature is { } temp && !modelId.Contains("fable") && !modelId.Contains("mythos"))
                request["temperature"] = temp;
            if (tools is { Count: > 0 }) request["tools"] = tools.DeepClone();

            using var httpReq = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            httpReq.Headers.Add("x-api-key", apiKey);
            httpReq.Headers.Add("anthropic-version", "2023-06-01");
            httpReq.Content = new StringContent(request.ToJsonString(J), Encoding.UTF8, "application/json");

            using var resp = await http.SendAsync(httpReq, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
                throw new KernelException($"Anthropic API error {(int)resp.StatusCode}: {body}");

            var root = JsonNode.Parse(body)!.AsObject();
            var contentBlocks = root["content"]!.AsArray();
            var stopReason = root["stop_reason"]?.GetValue<string>();

            // Collect assistant text + any tool calls.
            var assistantText = new StringBuilder();
            var toolUses = new List<(string Id, string Name, JsonNode Input)>();
            foreach (var block in contentBlocks)
            {
                var type = block!["type"]!.GetValue<string>();
                if (type == "text") assistantText.Append(block["text"]!.GetValue<string>());
                else if (type == "tool_use")
                    toolUses.Add((block["id"]!.GetValue<string>(), block["name"]!.GetValue<string>(),
                        block["input"] ?? new JsonObject()));
            }

            if (stopReason != "tool_use" || toolUses.Count == 0 || !autoInvoke)
                return new[] { new ChatMessageContent(AuthorRole.Assistant, assistantText.ToString()) };

            // Echo assistant turn (with tool_use blocks) then execute tools and feed results back.
            messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = contentBlocks.DeepClone() });
            var toolResults = new JsonArray();
            foreach (var (id, name, input) in toolUses)
            {
                var result = await InvokeKernelFunctionAsync(kernel!, name, input, cancellationToken);
                toolResults.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = id,
                    ["content"] = result
                });
            }
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = toolResults });
        }

        return new[] { new ChatMessageContent(AuthorRole.Assistant,
            "Maaf, terlalu banyak langkah pemanggilan fungsi. Coba persempit pertanyaannya.") };
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Non-streaming fallback: run the full request then yield the final text once.
        var results = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        foreach (var r in results)
            yield return new StreamingChatMessageContent(r.Role, r.Content);
    }

    private static JsonObject ToAnthropicMessage(ChatMessageContent msg)
    {
        var role = msg.Role == AuthorRole.Assistant ? "assistant" : "user";
        var content = new JsonArray();
        foreach (var item in msg.Items)
        {
            switch (item)
            {
                case TextContent t when !string.IsNullOrEmpty(t.Text):
                    content.Add(new JsonObject { ["type"] = "text", ["text"] = t.Text });
                    break;
                case ImageContent img when img.Uri is not null:
                    content.Add(new JsonObject
                    {
                        ["type"] = "image",
                        ["source"] = new JsonObject { ["type"] = "url", ["url"] = img.Uri.ToString() }
                    });
                    break;
            }
        }
        if (content.Count == 0) content.Add(new JsonObject { ["type"] = "text", ["text"] = msg.Content ?? "" });
        return new JsonObject { ["role"] = role, ["content"] = content };
    }

    private static JsonArray BuildTools(Kernel kernel)
    {
        var tools = new JsonArray();
        foreach (var plugin in kernel.Plugins)
        foreach (var fn in plugin)
        {
            var props = new JsonObject();
            var required = new JsonArray();
            foreach (var p in fn.Metadata.Parameters)
            {
                props[p.Name] = new JsonObject
                {
                    ["type"] = MapType(p.ParameterType),
                    ["description"] = p.Description ?? ""
                };
                if (p.IsRequired) required.Add(p.Name);
            }
            tools.Add(new JsonObject
            {
                ["name"] = $"{plugin.Name}-{fn.Name}",
                ["description"] = fn.Description ?? fn.Name,
                ["input_schema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = props,
                    ["required"] = required
                }
            });
        }
        return tools;
    }

    private static string MapType(Type? t) => t switch
    {
        _ when t == typeof(int) || t == typeof(long) => "integer",
        _ when t == typeof(double) || t == typeof(float) || t == typeof(decimal) => "number",
        _ when t == typeof(bool) => "boolean",
        _ => "string"
    };

    private static async Task<string> InvokeKernelFunctionAsync(Kernel kernel, string qualifiedName, JsonNode input, CancellationToken ct)
    {
        try
        {
            var parts = qualifiedName.Split('-', 2);
            var (pluginName, fnName) = parts.Length == 2 ? (parts[0], parts[1]) : ("", parts[0]);
            var fn = kernel.Plugins.GetFunction(string.IsNullOrEmpty(pluginName) ? null : pluginName, fnName);
            var args = new KernelArguments();
            if (input is JsonObject obj)
                foreach (var kv in obj)
                    args[kv.Key] = kv.Value?.GetValueKind() switch
                    {
                        JsonValueKind.Number => kv.Value!.GetValue<double>(),
                        JsonValueKind.True or JsonValueKind.False => kv.Value!.GetValue<bool>(),
                        _ => kv.Value?.ToString()
                    };
            var result = await fn.InvokeAsync(kernel, args, ct);
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error menjalankan fungsi '{qualifiedName}': {ex.Message}";
        }
    }
}
