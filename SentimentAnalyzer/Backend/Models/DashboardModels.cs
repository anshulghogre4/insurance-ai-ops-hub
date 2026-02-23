namespace SentimentAnalyzer.API.Models;

public class DashboardMetrics
{
    public int TotalAnalyses { get; set; }
    public double AvgPurchaseIntent { get; set; }
    public double AvgSentimentScore { get; set; }
    public int HighRiskCount { get; set; }
}

public class SentimentDistribution
{
    public double Positive { get; set; }
    public double Negative { get; set; }
    public double Neutral { get; set; }
    public double Mixed { get; set; }
}

public class PersonaCount
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class DashboardData
{
    public DashboardMetrics Metrics { get; set; } = new();
    public SentimentDistribution SentimentDistribution { get; set; } = new();
    public List<PersonaCount> TopPersonas { get; set; } = [];
}

public class AnalysisHistoryItem
{
    public int Id { get; set; }
    public string InputTextPreview { get; set; } = string.Empty;
    public string Sentiment { get; set; } = string.Empty;
    public int PurchaseIntentScore { get; set; }
    public string CustomerPersona { get; set; } = string.Empty;
    public string InteractionType { get; set; } = string.Empty;
    public string ChurnRisk { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
