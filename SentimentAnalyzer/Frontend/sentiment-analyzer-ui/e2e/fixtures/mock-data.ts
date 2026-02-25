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

export const MOCK_CLAIM_TRIAGE_RESPONSE = {
  claimId: 101,
  severity: 'High',
  urgency: 'Immediate',
  claimType: 'Water Damage',
  fraudScore: 42,
  fraudRiskLevel: 'Medium',
  estimatedLossRange: '$5,000 - $15,000',
  recommendedActions: [
    { action: 'Assign field adjuster within 24 hours', priority: 'High', reasoning: 'Active water damage requires immediate assessment' },
    { action: 'Contact policyholder for additional photos', priority: 'Medium', reasoning: 'Document extent of damage' },
    { action: 'Schedule emergency mitigation', priority: 'High', reasoning: 'Prevent mold growth and further damage' }
  ],
  fraudFlags: ['Timing anomaly - claim filed within 30 days of policy inception', 'High claim amount relative to property value'],
  evidence: [],
  status: 'Triaged',
  createdAt: '2026-02-24T10:00:00Z'
};

export const MOCK_CLAIMS_HISTORY_RESPONSE = {
  items: [
    { claimId: 101, severity: 'High', urgency: 'Immediate', claimType: 'Water Damage', fraudScore: 42, fraudRiskLevel: 'Medium', estimatedLossRange: '$5K-$15K', recommendedActions: [], fraudFlags: [], evidence: [], status: 'Triaged', createdAt: '2026-02-24T10:00:00Z' },
    { claimId: 102, severity: 'Low', urgency: 'Standard', claimType: 'Auto Scratch', fraudScore: 12, fraudRiskLevel: 'Low', estimatedLossRange: '$500-$1,500', recommendedActions: [], fraudFlags: [], evidence: [], status: 'Resolved', createdAt: '2026-02-23T14:00:00Z' },
    { claimId: 103, severity: 'Critical', urgency: 'Emergency', claimType: 'Structure Fire', fraudScore: 78, fraudRiskLevel: 'High', estimatedLossRange: '$50K-$200K', recommendedActions: [], fraudFlags: ['Timing anomaly', 'Financial motive'], evidence: [], status: 'UnderReview', createdAt: '2026-02-22T08:00:00Z' }
  ],
  totalCount: 3, page: 1, pageSize: 20, totalPages: 1
};

export const MOCK_FRAUD_ALERTS_RESPONSE = [
  { claimId: 201, severity: 'Critical', urgency: 'Emergency', claimType: 'Structure Fire', fraudScore: 82, fraudRiskLevel: 'VeryHigh', estimatedLossRange: '$100K-$500K', recommendedActions: [{ action: 'Refer to SIU', priority: 'Critical', reasoning: 'Multiple fraud indicators' }], fraudFlags: ['Timing anomaly', 'Financial motive', 'Inconsistent documentation'], evidence: [], status: 'UnderReview', createdAt: '2026-02-24T08:00:00Z' },
  { claimId: 202, severity: 'High', urgency: 'Priority', claimType: 'Theft', fraudScore: 65, fraudRiskLevel: 'High', estimatedLossRange: '$10K-$25K', recommendedActions: [], fraudFlags: ['Pattern match with known fraud ring'], evidence: [], status: 'UnderReview', createdAt: '2026-02-23T14:00:00Z' }
];

export const MOCK_PROVIDER_HEALTH_RESPONSE = {
  llmProviders: [
    { name: 'Groq', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
    { name: 'Cerebras', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
    { name: 'Mistral', status: 'Degraded', isAvailable: true, consecutiveFailures: 2, cooldownSeconds: 0, cooldownExpiresUtc: null },
    { name: 'Gemini', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
    { name: 'OpenRouter', status: 'Down', isAvailable: false, consecutiveFailures: 5, cooldownSeconds: 120, cooldownExpiresUtc: '2026-02-24T10:05:00Z' },
    { name: 'OpenAI', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
    { name: 'Ollama', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null }
  ],
  multimodalServices: [
    { name: 'Deepgram STT', isConfigured: true, status: 'Available' },
    { name: 'Azure Vision', isConfigured: true, status: 'Available' },
    { name: 'Cloudflare Vision', isConfigured: true, status: 'Available' },
    { name: 'OCR.space', isConfigured: true, status: 'Available' },
    { name: 'HuggingFace NER', isConfigured: false, status: 'Not Configured' },
    { name: 'Voyage AI Embeddings', isConfigured: false, status: 'Not Configured' }
  ],
  checkedAt: '2026-02-24T10:00:00Z'
};

export const MOCK_EVIDENCE_RESPONSE = {
  evidenceType: 'image',
  provider: 'Azure Vision',
  processedText: 'Water damage visible on ceiling and walls. Mold growth detected in corners.',
  damageIndicators: ['water staining', 'mold growth', 'structural damage'],
  createdAt: '2026-02-24T10:05:00Z'
};

export const MOCK_FRAUD_ANALYSIS_RESPONSE = {
  claimId: 101,
  fraudScore: 72,
  riskLevel: 'High',
  indicators: [
    { category: 'Timing', description: 'Claim filed within 30 days of policy inception', severity: 'High' },
    { category: 'Financial', description: 'Claim amount exceeds typical range for property type', severity: 'Medium' }
  ],
  recommendedActions: [{ action: 'Refer to SIU', priority: 'Critical', reasoning: 'Multiple high-severity indicators' }],
  referToSIU: true,
  siuReferralReason: 'Multiple high-severity fraud indicators detected',
  confidence: 0.85
};

export const CLAIMS_TEST_TEXTS = {
  waterDamage: 'Water pipe burst in basement causing significant flooding. Damage to flooring, drywall, and personal property. Policy HO-2024-789456.',
  autoAccident: 'Rear-end collision on Highway 101. Other driver ran red light. Police report filed. Vehicle has significant rear damage.',
  theftReport: 'Home burglary while traveling. Electronics, jewelry, and cash stolen totaling approximately $25,000. Police report filed.'
};
