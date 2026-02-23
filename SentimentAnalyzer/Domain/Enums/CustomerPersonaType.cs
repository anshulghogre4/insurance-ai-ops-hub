namespace SentimentAnalyzer.Domain.Enums;

/// <summary>
/// Insurance customer persona classifications based on behavioral patterns.
/// </summary>
public enum CustomerPersonaType
{
    /// <summary>Focused on cost, mentions budget, compares prices.</summary>
    PriceSensitive,

    /// <summary>Asks about coverage details, limits, exclusions.</summary>
    CoverageFocused,

    /// <summary>Had negative claim experience, expressing dissatisfaction.</summary>
    ClaimFrustrated,

    /// <summary>First-time insurance buyer, asks basic questions.</summary>
    NewBuyer,

    /// <summary>Existing customer showing signs of leaving.</summary>
    RenewalRisk,

    /// <summary>Satisfied customer interested in additional coverage.</summary>
    UpsellReady
}
