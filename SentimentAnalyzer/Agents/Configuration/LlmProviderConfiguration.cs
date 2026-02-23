namespace SentimentAnalyzer.Agents.Configuration;

/// <summary>
/// Configuration for the multi-agent LLM provider system.
/// Supports capability-based routing with automatic fallback chain.
/// </summary>
public class AgentSystemSettings
{
    /// <summary>Primary provider: "Groq", "Gemini", "Ollama", "Mistral", "OpenRouter", or "OpenAI".</summary>
    public string Provider { get; set; } = "Groq";

    /// <summary>Ordered fallback chain for text LLM providers. First healthy provider is used.</summary>
    public List<string> FallbackChain { get; set; } = ["Groq", "Mistral", "Gemini", "OpenRouter", "Ollama"];

    /// <summary>Groq API configuration.</summary>
    public GroqSettings Groq { get; set; } = new();

    /// <summary>Ollama local inference configuration.</summary>
    public OllamaSettings Ollama { get; set; } = new();

    /// <summary>Gemini API configuration.</summary>
    public GeminiSettings Gemini { get; set; } = new();

    /// <summary>Mistral API configuration (1B tokens/month free).</summary>
    public MistralSettings Mistral { get; set; } = new();

    /// <summary>OpenRouter API configuration (24+ free models).</summary>
    public OpenRouterSettings OpenRouter { get; set; } = new();

    /// <summary>Deepgram speech-to-text configuration ($200 credit).</summary>
    public DeepgramSettings Deepgram { get; set; } = new();

    /// <summary>Cloudflare Workers AI configuration (multimodal vision).</summary>
    public CloudflareSettings Cloudflare { get; set; } = new();

    /// <summary>Azure AI Vision F0 configuration (5K txn/month free).</summary>
    public AzureVisionSettings AzureVision { get; set; } = new();

    /// <summary>OCR.space document OCR configuration (500 req/day free).</summary>
    public OcrSpaceSettings OcrSpace { get; set; } = new();

    /// <summary>HuggingFace Inference API configuration (NER, classification).</summary>
    public HuggingFaceSettings HuggingFace { get; set; } = new();
}

/// <summary>
/// Groq API settings (OpenAI-compatible endpoint).
/// </summary>
public class GroqSettings
{
    /// <summary>Groq API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Groq API endpoint (OpenAI-compatible).</summary>
    public string Endpoint { get; set; } = "https://api.groq.com/openai/v1";

    /// <summary>Model to use (e.g., "llama-3.3-70b-versatile").</summary>
    public string Model { get; set; } = "llama-3.3-70b-versatile";
}

/// <summary>
/// Ollama local inference settings.
/// </summary>
public class OllamaSettings
{
    /// <summary>Ollama endpoint (default local).</summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>Model to use (e.g., "llama3.2").</summary>
    public string Model { get; set; } = "llama3.2";
}

/// <summary>
/// Google Gemini API settings.
/// </summary>
public class GeminiSettings
{
    /// <summary>Gemini API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model to use.</summary>
    public string Model { get; set; } = "gemini-2.5-flash";
}

/// <summary>
/// Mistral API settings (OpenAI-compatible endpoint).
/// Free tier: 1 billion tokens/month.
/// </summary>
public class MistralSettings
{
    /// <summary>Mistral API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Mistral API endpoint (OpenAI-compatible).</summary>
    public string Endpoint { get; set; } = "https://api.mistral.ai/v1";

    /// <summary>Model to use (e.g., "mistral-large-latest").</summary>
    public string Model { get; set; } = "mistral-large-latest";
}

/// <summary>
/// OpenRouter API settings (OpenAI-compatible endpoint, 24+ free models).
/// Free tier: 50 req/day on free models.
/// </summary>
public class OpenRouterSettings
{
    /// <summary>OpenRouter API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>OpenRouter API endpoint (OpenAI-compatible).</summary>
    public string Endpoint { get; set; } = "https://openrouter.ai/api/v1";

    /// <summary>Model to use (e.g., "deepseek/deepseek-r1:free").</summary>
    public string Model { get; set; } = "deepseek/deepseek-r1:free";

    /// <summary>Required by OpenRouter: HTTP-Referer header for identification.</summary>
    public string SiteUrl { get; set; } = "http://localhost:5143";
}

/// <summary>
/// Deepgram speech-to-text settings.
/// Free tier: $200 credit (no expiry), ~3,300 hours of transcription.
/// </summary>
public class DeepgramSettings
{
    /// <summary>Deepgram API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Deepgram API endpoint.</summary>
    public string Endpoint { get; set; } = "https://api.deepgram.com/v1";

    /// <summary>Transcription model (e.g., "nova-2").</summary>
    public string Model { get; set; } = "nova-2";
}

/// <summary>
/// Cloudflare Workers AI settings (multimodal vision).
/// Free tier: 10,000 neurons/day.
/// </summary>
public class CloudflareSettings
{
    /// <summary>Cloudflare API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Cloudflare account ID.</summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>Vision model to use.</summary>
    public string VisionModel { get; set; } = "@cf/meta/llama-4-scout-17b-16e-instruct";
}

/// <summary>
/// Azure AI Vision (Computer Vision) F0 free tier settings.
/// Free tier: 5,000 transactions/month. Blocks at limit (429), never charges.
/// </summary>
public class AzureVisionSettings
{
    /// <summary>Azure Vision API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Azure Vision endpoint URL (e.g., "https://your-resource.cognitiveservices.azure.com/").</summary>
    public string Endpoint { get; set; } = string.Empty;
}

/// <summary>
/// OCR.space document OCR settings.
/// Free tier: 500 requests/day.
/// </summary>
public class OcrSpaceSettings
{
    /// <summary>OCR.space API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>OCR.space API endpoint.</summary>
    public string Endpoint { get; set; } = "https://api.ocr.space/parse/image";
}

/// <summary>
/// HuggingFace Inference API settings (NER, classification).
/// Free tier: rate-limited (300 requests/hour).
/// </summary>
public class HuggingFaceSettings
{
    /// <summary>HuggingFace API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>NER model for entity extraction.</summary>
    public string NerModel { get; set; } = "dslim/bert-base-NER";
}
