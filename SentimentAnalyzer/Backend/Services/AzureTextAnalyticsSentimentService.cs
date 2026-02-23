using Azure;
using Azure.AI.TextAnalytics;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services;

/// <summary>
/// Sentiment analysis service using Azure Text Analytics (FREE tier: 5,000 requests/month)
/// </summary>
public class AzureTextAnalyticsSentimentService : ISentimentService
{
    private readonly TextAnalyticsClient _client;
    private readonly ILogger<AzureTextAnalyticsSentimentService> _logger;

    public AzureTextAnalyticsSentimentService(
        IConfiguration configuration,
        ILogger<AzureTextAnalyticsSentimentService> logger)
    {
        _logger = logger;

        var endpoint = configuration["Azure:TextAnalytics:Endpoint"]
            ?? throw new ArgumentNullException("Azure:TextAnalytics:Endpoint configuration is missing");

        var apiKey = configuration["Azure:TextAnalytics:ApiKey"]
            ?? throw new ArgumentNullException("Azure:TextAnalytics:ApiKey configuration is missing");

        _client = new TextAnalyticsClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<SentimentResponse> AnalyzeSentimentAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be empty", nameof(text));
        }

        try
        {
            _logger.LogInformation("Sending request to Azure Text Analytics for text: {TextPreview}",
                text.Length > 50 ? text[..50] + "..." : text);

            // Call Azure Text Analytics
            var response = await _client.AnalyzeSentimentAsync(text);
            var documentSentiment = response.Value;

            _logger.LogInformation("Azure Response - Sentiment: {Sentiment}, Confidence: Positive={Positive}, Neutral={Neutral}, Negative={Negative}",
                documentSentiment.Sentiment,
                documentSentiment.ConfidenceScores.Positive,
                documentSentiment.ConfidenceScores.Neutral,
                documentSentiment.ConfidenceScores.Negative);

            // Map Azure sentiment to our format
            var sentiment = MapAzureSentiment(documentSentiment.Sentiment);
            var confidenceScore = GetConfidenceScore(documentSentiment.ConfidenceScores, sentiment);

            // Generate explanation based on sentiment and confidence
            var explanation = GenerateExplanation(sentiment, confidenceScore, documentSentiment);

            // Create emotion breakdown from Azure's confidence scores
            var emotionBreakdown = new Dictionary<string, double>
            {
                { "positive", documentSentiment.ConfidenceScores.Positive },
                { "neutral", documentSentiment.ConfidenceScores.Neutral },
                { "negative", documentSentiment.ConfidenceScores.Negative }
            };

            return new SentimentResponse
            {
                Sentiment = sentiment,
                ConfidenceScore = confidenceScore,
                Explanation = explanation,
                EmotionBreakdown = emotionBreakdown
            };
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Text Analytics API error: {Message}", ex.Message);
            throw new InvalidOperationException($"Azure Text Analytics API error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error analyzing sentiment");
            throw;
        }
    }

    private static string MapAzureSentiment(TextSentiment azureSentiment)
    {
        return azureSentiment switch
        {
            TextSentiment.Positive => "Positive",
            TextSentiment.Negative => "Negative",
            TextSentiment.Neutral => "Neutral",
            TextSentiment.Mixed => "Neutral", // Treat mixed as neutral
            _ => "Neutral"
        };
    }

    private static double GetConfidenceScore(SentimentConfidenceScores scores, string sentiment)
    {
        return sentiment switch
        {
            "Positive" => scores.Positive,
            "Negative" => scores.Negative,
            "Neutral" => scores.Neutral,
            _ => scores.Neutral
        };
    }

    private static string GenerateExplanation(string sentiment, double confidence, DocumentSentiment documentSentiment)
    {
        var confidenceLevel = confidence switch
        {
            >= 0.9 => "very high",
            >= 0.7 => "high",
            >= 0.5 => "moderate",
            _ => "low"
        };

        var explanation = $"The text expresses a {sentiment.ToLower()} sentiment with {confidenceLevel} confidence ({confidence:P0}).";

        // Add details about sentence-level sentiment if available
        if (documentSentiment.Sentences.Count > 1)
        {
            var sentenceSentiments = documentSentiment.Sentences
                .GroupBy(s => s.Sentiment)
                .ToDictionary(g => g.Key, g => g.Count());

            explanation += $" Analysis of {documentSentiment.Sentences.Count} sentences found: ";
            explanation += string.Join(", ", sentenceSentiments.Select(kvp => $"{kvp.Value} {kvp.Key.ToString().ToLower()}"));
            explanation += ".";
        }

        return explanation;
    }
}
