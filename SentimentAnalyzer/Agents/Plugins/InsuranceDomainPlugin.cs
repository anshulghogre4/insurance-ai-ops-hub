using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SentimentAnalyzer.Agents.Plugins;

/// <summary>
/// Semantic Kernel plugin providing insurance domain reference data to agents.
/// </summary>
public class InsuranceDomainPlugin
{
    /// <summary>
    /// Returns the available insurance product categories for recommendations.
    /// </summary>
    [KernelFunction, Description("Get available insurance product categories for policy recommendations")]
    public string GetInsuranceProducts()
    {
        return """
            Available Insurance Products:
            - Auto Insurance: Liability, Collision, Comprehensive, Uninsured Motorist
            - Home Insurance: HO-3 (Standard), HO-5 (Premium), HO-6 (Condo), Renters (HO-4)
            - Health Insurance: Bronze, Silver, Gold, Platinum plans, HSA-eligible
            - Life Insurance: Term Life, Whole Life, Universal Life
            - Commercial Insurance: General Liability, Professional Liability, Workers Comp, BOP
            - Umbrella Insurance: Personal Umbrella, Commercial Umbrella
            - Specialty: Flood, Earthquake, Pet, Travel, Cyber
            """;
    }

    /// <summary>
    /// Returns customer persona descriptions for classification.
    /// </summary>
    [KernelFunction, Description("Get customer persona descriptions for classification")]
    public string GetPersonaDescriptions()
    {
        return """
            Customer Personas:
            - PriceSensitive: Budget-focused, compares quotes, mentions cost/price frequently, asks about discounts
            - CoverageFocused: Asks detailed coverage questions, wants to understand limits/exclusions, values protection over price
            - ClaimFrustrated: Expressing dissatisfaction with claim process, mentions delays, denied claims, poor service
            - NewBuyer: First-time buyer, asks basic questions, may not know terminology, needs guidance
            - RenewalRisk: At renewal, received competitor quote, threatening to leave, asking for price match
            - UpsellReady: Satisfied existing customer, asking about additional products, wants to consolidate
            """;
    }

    /// <summary>
    /// Returns insurance-specific complaint keywords for detection.
    /// </summary>
    [KernelFunction, Description("Get complaint detection keywords for insurance domain")]
    public string GetComplaintKeywords()
    {
        return """
            High-severity complaint indicators:
            - "file a complaint", "department of insurance", "state insurance commissioner"
            - "attorney", "lawyer", "lawsuit", "legal action", "sue"
            - "BBB", "Better Business Bureau", "consumer protection"
            - "worst experience", "unacceptable", "demand to speak with supervisor"
            - "regulatory", "violation", "bad faith"
            """;
    }
}
