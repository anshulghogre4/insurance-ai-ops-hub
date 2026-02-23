/** Realistic insurance mock data for e2e tests. */

export const MOCK_INSURANCE_ANALYSIS_RESPONSE = {
  sentiment: 'Negative',
  confidenceScore: 0.92,
  explanation:
    'The policyholder expresses significant frustration regarding delayed claim processing. Language indicates high dissatisfaction with service response times and communication gaps.',
  emotionBreakdown: {
    frustration: 0.85,
    anger: 0.7,
    disappointment: 0.65,
    anxiety: 0.4,
    hope: 0.1,
  },
  insuranceAnalysis: {
    purchaseIntentScore: 15,
    customerPersona: 'ClaimFrustrated',
    journeyStage: 'ActiveClaim',
    riskIndicators: {
      churnRisk: 'High',
      complaintEscalationRisk: 'High',
      fraudIndicators: 'None',
    },
    policyRecommendations: [
      {
        product: 'Claims Priority Service',
        reasoning:
          'Expedited claims processing to address frustration with delays',
      },
      {
        product: 'Enhanced Communication Package',
        reasoning: 'Proactive status updates to reduce anxiety and uncertainty',
      },
    ],
    interactionType: 'Complaint',
    keyTopics: ['claim delay', 'no response', 'switching providers', 'water damage'],
  },
  quality: {
    isValid: true,
    qualityScore: 88,
    issues: [
      {
        severity: 'warning',
        field: 'sentiment',
        message: 'High negative sentiment may warrant immediate supervisor review',
      },
    ],
    suggestions: [
      'Add customer ID for personalized retention recommendations',
    ],
    warnings: [
      '[warning] sentiment: High negative sentiment may warrant immediate supervisor review',
      'Add customer ID for personalized retention recommendations',
    ],
  },
};

export const MOCK_SENTIMENT_V1_RESPONSE = {
  sentiment: 'Positive',
  confidenceScore: 0.89,
  explanation:
    'The text conveys strong satisfaction with the insurance claim process and agent responsiveness.',
  emotionBreakdown: {
    satisfaction: 0.82,
    gratitude: 0.75,
    relief: 0.6,
    trust: 0.55,
  },
};

export const MOCK_DASHBOARD_RESPONSE = {
  metrics: {
    totalAnalyses: 42,
    avgSentimentScore: 0.65,
    avgPurchaseIntent: 48,
    highRiskCount: 7,
  },
  sentimentDistribution: {
    positive: 35,
    negative: 30,
    neutral: 20,
    mixed: 15,
  },
  topPersonas: [
    { name: 'ClaimFrustrated', count: 15, percentage: 36 },
    { name: 'RenewalRisk', count: 10, percentage: 24 },
    { name: 'PriceSensitive', count: 8, percentage: 19 },
    { name: 'CoverageFocused', count: 5, percentage: 12 },
    { name: 'UpsellReady', count: 4, percentage: 9 },
  ],
};

export const MOCK_HISTORY_RESPONSE = [
  {
    id: 1,
    inputTextPreview: 'I reported water damage on Jan 15...',
    sentiment: 'Negative',
    purchaseIntentScore: 15,
    customerPersona: 'ClaimFrustrated',
    churnRisk: 'High',
    interactionType: 'Complaint',
    createdAt: '2026-02-18T10:30:00Z',
  },
  {
    id: 2,
    inputTextPreview: 'Very satisfied with my policy renewal...',
    sentiment: 'Positive',
    purchaseIntentScore: 72,
    customerPersona: 'CoverageFocused',
    churnRisk: 'Low',
    interactionType: 'General',
    createdAt: '2026-02-17T14:20:00Z',
  },
  {
    id: 3,
    inputTextPreview: 'Need to compare rates before renewing...',
    sentiment: 'Neutral',
    purchaseIntentScore: 55,
    customerPersona: 'PriceSensitive',
    churnRisk: 'Medium',
    interactionType: 'Email',
    createdAt: '2026-02-16T09:15:00Z',
  },
];

/** Realistic insurance input texts for testing. */
export const INSURANCE_TEST_TEXTS = {
  claimComplaint:
    'I reported water damage to my kitchen on January 15th under policy HO-2024-789456. ' +
    "It's been three weeks and I haven't received any response from the adjuster. " +
    "If this isn't resolved by Friday, I'm switching to another provider.",
  positiveReview:
    'My agent Sarah was incredibly helpful during my auto claim. She guided me through every step ' +
    'and the settlement was processed in just 5 business days. Best insurance experience I have ever had.',
  billingDispute:
    'I was charged $847 instead of the quoted $695 for my homeowners premium. ' +
    'I called three times and each representative gave me different information. ' +
    'Please correct the billing immediately or I will file a complaint with the state insurance department.',
};
