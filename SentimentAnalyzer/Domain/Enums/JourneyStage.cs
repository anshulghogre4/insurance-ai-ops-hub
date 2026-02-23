namespace SentimentAnalyzer.Domain.Enums;

/// <summary>
/// Customer journey stage in the insurance lifecycle.
/// </summary>
public enum JourneyStage
{
    /// <summary>Learning about insurance needs.</summary>
    Awareness,

    /// <summary>Actively researching options.</summary>
    Consideration,

    /// <summary>Ready to make a purchase choice.</summary>
    Decision,

    /// <summary>New policyholder getting started.</summary>
    Onboarding,

    /// <summary>Currently filing or managing a claim.</summary>
    ActiveClaim,

    /// <summary>Approaching or in renewal period.</summary>
    Renewal
}
