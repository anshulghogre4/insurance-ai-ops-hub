namespace SentimentAnalyzer.Agents.Configuration;

/// <summary>
/// Configuration for the multi-agent LLM provider system.
/// Supports capability-based routing with automatic fallback chain.
/// </summary>
public class AgentSystemSettings
{
    /// <summary>Primary provider: "Groq", "Gemini", "Ollama", "Mistral", "OpenRouter", "OpenAI", or "Cerebras".</summary>
    public string Provider { get; set; } = "Groq";

    /// <summary>Ordered fallback chain for text LLM providers. First healthy provider is used.</summary>
    public List<string> FallbackChain { get; set; } = ["Groq", "Cerebras", "Mistral", "Gemini", "OpenRouter", "OpenAI", "Ollama"];

    /// <summary>Groq API configuration.</summary>
    public GroqSettings Groq { get; set; } = new();

    /// <summary>Ollama local inference configuration.</summary>
    public OllamaSettings Ollama { get; set; } = new();

    /// <summary>Gemini API configuration.</summary>
    public GeminiSettings Gemini { get; set; } = new();

    /// <summary>Mistral API configuration (1B tokens/month free).</summary>
    public MistralSettings Mistral { get; set; } = new();

    /// <summary>Cerebras API configuration (1M tokens/day free, 2,600 tok/s).</summary>
    public CerebrasSettings Cerebras { get; set; } = new();

    /// <summary>OpenRouter API configuration (24+ free models).</summary>
    public OpenRouterSettings OpenRouter { get; set; } = new();

    /// <summary>Deepgram speech-to-text configuration ($200 credit).</summary>
    public DeepgramSettings Deepgram { get; set; } = new();

    /// <summary>Cloudflare Workers AI configuration (multimodal vision).</summary>
    public CloudflareSettings Cloudflare { get; set; } = new();

    /// <summary>Azure AI Vision F0 configuration (5K txn/month free).</summary>
    public AzureVisionSettings AzureVision { get; set; } = new();

    /// <summary>Azure AI Document Intelligence F0 configuration (500 pages/month free, best OCR accuracy).</summary>
    public AzureDocumentIntelligenceSettings AzureDocumentIntelligence { get; set; } = new();

    /// <summary>OCR.space document OCR configuration (500 req/day free).</summary>
    public OcrSpaceSettings OcrSpace { get; set; } = new();

    /// <summary>HuggingFace Inference API configuration (NER, classification).</summary>
    public HuggingFaceSettings HuggingFace { get; set; } = new();

    /// <summary>Voyage AI embeddings configuration (finance-domain RAG, 50M tokens free).</summary>
    public VoyageSettings Voyage { get; set; } = new();

    /// <summary>Azure AI Language configuration (NER, 5K text records/month free F0).</summary>
    public AzureLanguageSettings AzureLanguage { get; set; } = new();

    /// <summary>Azure AI Content Safety configuration (5K text + 5K image/month free F0).</summary>
    public AzureContentSafetySettings AzureContentSafety { get; set; } = new();

    /// <summary>Azure AI Translator configuration (2M chars/month free F0).</summary>
    public AzureTranslatorSettings AzureTranslator { get; set; } = new();

    /// <summary>Azure AI Speech configuration (5 hrs STT/month free F0).</summary>
    public AzureSpeechSettings AzureSpeech { get; set; } = new();
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

    /// <summary>Model to use (e.g., "google/gemma-3-27b-it:free").</summary>
    public string Model { get; set; } = "google/gemma-3-27b-it:free";

    /// <summary>Required by OpenRouter: HTTP-Referer header for identification.</summary>
    public string SiteUrl { get; set; } = "http://localhost:5143";
}

/// <summary>
/// Cerebras API settings (OpenAI-compatible endpoint).
/// Free tier: 1M tokens/day, 30 RPM, 60K TPM. Fastest inference (~2,600 tok/s).
/// </summary>
public class CerebrasSettings
{
    /// <summary>Cerebras API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Cerebras API endpoint (OpenAI-compatible).</summary>
    public string Endpoint { get; set; } = "https://api.cerebras.ai/v1";

    /// <summary>Model to use (e.g., "gpt-oss-120b").</summary>
    public string Model { get; set; } = "gpt-oss-120b";
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
/// Azure AI Document Intelligence settings (prebuilt-read model).
/// Free F0 tier: 500 pages/month, max 2 pages/request, 4MB max file size, 1 req/sec.
/// Insurance use case: highest-accuracy OCR for scanned policy documents and claim forms.
/// </summary>
public class AzureDocumentIntelligenceSettings
{
    /// <summary>Azure Document Intelligence API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Azure Document Intelligence endpoint URL (e.g., "https://your-resource.cognitiveservices.azure.com/").</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Document analysis model to use (e.g., "prebuilt-read" for general OCR).</summary>
    public string Model { get; set; } = "prebuilt-read";
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

    /// <summary>Sentiment model for financial text pre-screening (FinBERT).</summary>
    public string SentimentModel { get; set; } = "ProsusAI/finbert";

    /// <summary>
    /// Confidence threshold for FinBERT pre-screening. When the top sentiment score
    /// exceeds this threshold, the full multi-agent pipeline is skipped.
    /// Range: 0.0 to 1.0. Default: 0.85.
    /// </summary>
    public double PreScreenConfidenceThreshold { get; set; } = 0.85;
}

/// <summary>
/// Voyage AI embeddings settings (finance-domain RAG).
/// Free tier: 50M tokens for voyage-finance-2, 200M for general models. No credit card required.
/// </summary>
public class VoyageSettings
{
    /// <summary>Voyage AI API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Voyage AI API endpoint.</summary>
    public string Endpoint { get; set; } = "https://api.voyageai.com/v1";

    /// <summary>Embedding model to use (e.g., "voyage-finance-2" for insurance/finance domain).</summary>
    public string Model { get; set; } = "voyage-finance-2";
}

/// <summary>
/// Azure AI Language settings (NER entity extraction).
/// Free F0 tier: 5,000 text records/month. Hard cap — 429 after limit.
/// </summary>
public class AzureLanguageSettings
{
    /// <summary>Azure Language API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Azure Language endpoint URL (e.g., "https://your-resource.cognitiveservices.azure.com/").</summary>
    public string Endpoint { get; set; } = string.Empty;
}

/// <summary>
/// Azure AI Content Safety settings (text and image moderation).
/// Free F0 tier: 5,000 text + 5,000 image analyses/month. Hard cap — 429 after limit.
/// </summary>
public class AzureContentSafetySettings
{
    /// <summary>Azure Content Safety API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Azure Content Safety endpoint URL (e.g., "https://your-resource.cognitiveservices.azure.com/").</summary>
    public string Endpoint { get; set; } = string.Empty;
}

/// <summary>
/// Azure AI Translator settings (multilingual text translation).
/// Free F0 tier: 2M characters/month. Hard cap — 429 after limit.
/// </summary>
public class AzureTranslatorSettings
{
    /// <summary>Azure Translator API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Azure Translator endpoint URL.</summary>
    public string Endpoint { get; set; } = "https://api.cognitive.microsofttranslator.com";

    /// <summary>Azure region for the Translator resource (e.g., "eastus").</summary>
    public string Region { get; set; } = string.Empty;
}

/// <summary>
/// Azure AI Speech settings (speech-to-text transcription).
/// Free F0 tier: 5 hours STT/month + 500K TTS chars/month. Hard cap — 429 after limit.
/// </summary>
public class AzureSpeechSettings
{
    /// <summary>Azure Speech API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Azure region for the Speech resource (e.g., "eastus").</summary>
    public string Region { get; set; } = string.Empty;
}
