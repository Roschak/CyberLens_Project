using CyberLens.Models;
using CyberLens.Services.Chat.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

#pragma warning disable SKEXP0070 // Gemini & Ollama connectors are experimental

namespace CyberLens.Services.Chat;

/// <summary>
/// Builds a Semantic Kernel wired to the LLM provider chosen in settings
/// (OpenAI, Anthropic, Gemini, or Ollama) with all Bang Kevin plugins loaded.
/// </summary>
public class AiKernelFactory(
    AppSettingsService settings,
    IServiceProvider services,
    IHttpClientFactory httpFactory)
{
    public string ActiveModel => settings.Current.Ai.Provider switch
    {
        "Anthropic" => settings.Current.Ai.AnthropicModel,
        "Gemini" => settings.Current.Ai.GeminiModel,
        "Ollama" => settings.Current.Ai.OllamaModel,
        _ => settings.Current.Ai.OpenAIModel
    };

    public Kernel Build()
    {
        var ai = settings.Current.Ai;
        var builder = Kernel.CreateBuilder();

        switch (ai.Provider)
        {
            case "Anthropic":
                builder.Services.AddSingleton<IChatCompletionService>(_ =>
                    new AnthropicChatCompletionService(httpFactory.CreateClient("ai"),
                        ai.AnthropicApiKey, ai.AnthropicModel));
                break;
            case "Gemini":
                builder.AddGoogleAIGeminiChatCompletion(ai.GeminiModel, ai.GeminiApiKey);
                break;
            case "Ollama":
                builder.AddOllamaChatCompletion(ai.OllamaModel, new Uri(ai.OllamaEndpoint));
                break;
            default: // OpenAI
                builder.AddOpenAIChatCompletion(ai.OpenAIModel, ai.OpenAIApiKey);
                break;
        }

        var kernel = builder.Build();

        // Plugins resolve their own dependencies from DI.
        kernel.Plugins.AddFromObject(ActivatorUtilities.CreateInstance<UtilityPlugin>(services), "Utility");
        kernel.Plugins.AddFromObject(ActivatorUtilities.CreateInstance<WebToolsPlugin>(services), "Web");
        kernel.Plugins.AddFromObject(ActivatorUtilities.CreateInstance<OsintDataPlugin>(services), "Osint");
        return kernel;
    }
}
