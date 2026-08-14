# Bang Kevin — AI intelligence assistant

Bang Kevin is a chat assistant built on **Microsoft Semantic Kernel**. It answers questions about the platform's OSINT data using real numbers (via tools) and can search the internet. Access it from the **Bang Kevin** page.

## Providers

Choose the LLM provider in **Settings → Bang Kevin**:

| Provider | Notes |
|----------|-------|
| OpenAI | native Semantic Kernel connector |
| Anthropic (Claude) | custom connector with a tool-use loop and image support |
| Gemini | Google AI connector |
| Ollama | local models via the Ollama endpoint |

Set the provider's API key (or Ollama endpoint), model, temperature, and system prompt (persona). Changes apply immediately.

## Chat features

- **Multi-session** — create, delete, and reset sessions. The first message becomes the session title.
- **Example prompts** — clickable starter prompts appear in the empty state and above the input; clicking one creates a session and sends it.
- **Attachments** — attach images (uploaded, then passed to the model as image content) and documents (uploaded, then linked in the message text). Files go through the configured storage backend.
- **Markdown rendering** — replies render tables, media (image/video/audio), code blocks, and links.

## Kernel functions (tools)

The assistant can call these automatically:

**Utility** — current date/time (UTC + WIB), arithmetic calculator, days-between-dates.

**Web** — `SearchInternet` (Tavily; needs a Tavily key), `ScrapePage` (extract readable text from a URL), `ReadFileFromUrl`.

**OSINT data** — `GetOverview`, `GetSentiment`, `GetTrendingTopics`, `SearchPosts`, `GetCategoryStats`, `GetWatchKeywords`, `GetRecentAlerts`, `GetSources`, `PredictTrend`, `GetTopEntities`. These query the platform's own database and analytics so answers are grounded in real data.

## Example prompts

- "Ringkas tren pemberitaan ransomware minggu ini."
- "Bagaimana sebaran sentimen 7 hari terakhir?"
- "Cari post tentang banjir dan sebutkan sumbernya."
- "Prediksi volume post 7 hari ke depan untuk kata kunci pemilu."
- "Entitas apa yang paling banyak disebut di jaringan?"

## Notes

- Without a valid API key, the chat returns a friendly configuration message pointing to Settings.
- Image attachments are fetched by URL by the model, so the app must be reachable at the URL it was started on (handled automatically for local runs).
