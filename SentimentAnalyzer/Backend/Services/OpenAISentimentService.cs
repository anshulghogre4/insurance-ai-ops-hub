using OpenAI.Chat;
using SentimentAnalyzer.API.Models;
using System.Text.Json;

namespace SentimentAnalyzer.API.Services;

public class OpenAISentimentService : ISentimentService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<OpenAISentimentService> _logger;

    public OpenAISentimentService(IConfiguration configuration, ILogger<OpenAISentimentService> logger)
    {
        _logger = logger;
        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new ArgumentNullException("OpenAI:ApiKey configuration is missing");

        var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        _chatClient = new ChatClient(model, apiKey);
    }

    public async Task<SentimentResponse> AnalyzeSentimentAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be empty", nameof(text));
        }

        try
        {
            var systemPrompt = @"You are a sentiment analysis expert. Analyze the given text and return a JSON response with:
1. sentiment: One of 'Positive', 'Negative', or 'Neutral'
2. confidenceScore: A number between 0 and 1 indicating confidence
3. explanation: A brief explanation of the sentiment
4. emotionBreakdown: An object with emotion names as keys and scores (0-1) as values (e.g., joy, sadness, anger, fear, surprise)

Return ONLY valid JSON, no additional text.";

            var userPrompt = $"Analyze this text: \"{text}\"";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            _logger.LogInformation("Sending request to OpenAI for text: {TextPreview}",
                text.Length > 50 ? text[..50] + "..." : text);

            var response = await _chatClient.CompleteChatAsync(messages);

            if (response?.Value?.Content == null || response.Value.Content.Count == 0)
            {
                throw new InvalidOperationException("Empty response from OpenAI API");
            }

            var content = response.Value.Content[0].Text;
            _logger.LogInformation("OpenAI Response: {Response}", content);

            // Try to extract JSON from response (in case there's extra text)
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                content = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }

            var sentimentResponse = JsonSerializer.Deserialize<SentimentResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (sentimentResponse == null)
            {
                _logger.LogWarning("Failed to deserialize OpenAI response");
                return new SentimentResponse
                {
                    Sentiment = "Neutral",
                    ConfidenceScore = 0.5,
                    Explanation = "Unable to analyze sentiment - invalid response format"
                };
            }

            // Validate response
            if (string.IsNullOrWhiteSpace(sentimentResponse.Sentiment))
            {
                sentimentResponse.Sentiment = "Neutral";
            }

            sentimentResponse.ConfidenceScore = Math.Clamp(sentimentResponse.ConfidenceScore, 0, 1);

            return sentimentResponse;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse OpenAI JSON response");
            throw new InvalidOperationException("Failed to parse sentiment analysis response", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling OpenAI API");
            throw new InvalidOperationException("Failed to connect to OpenAI API. Please check your API key and network connection.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error analyzing sentiment");
            throw;
        }
    }
}
