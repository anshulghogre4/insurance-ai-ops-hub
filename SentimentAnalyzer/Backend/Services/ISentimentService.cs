using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services;

public interface ISentimentService
{
    Task<SentimentResponse> AnalyzeSentimentAsync(string text);
}
