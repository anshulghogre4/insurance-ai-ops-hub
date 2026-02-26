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

// ===================== Document Intelligence RAG =====================

export const MOCK_DOCUMENT_UPLOAD_RESULT = {
  documentId: 501,
  fileName: 'homeowners-policy-2024.pdf',
  status: 'Processed',
  pageCount: 4,
  chunkCount: 12,
  embeddingProvider: 'Voyage AI',
  errorMessage: null
};

export const MOCK_DOCUMENT_QUERY_RESULT = {
  answer: 'Your homeowners policy covers water damage caused by sudden and accidental discharge from plumbing, heating, or air conditioning systems. The coverage limit for water damage is $250,000 per occurrence with a $1,000 deductible. However, flood damage from external sources requires separate flood insurance.',
  confidence: 0.87,
  citations: [
    {
      documentId: 501,
      fileName: 'homeowners-policy-2024.pdf',
      sectionName: 'COVERAGE A - DWELLING',
      chunkIndex: 2,
      relevantText: 'We cover sudden and accidental discharge or overflow of water from within a plumbing, heating, air conditioning or automatic fire protective sprinkler system.',
      similarity: 0.92
    },
    {
      documentId: 501,
      fileName: 'homeowners-policy-2024.pdf',
      sectionName: 'EXCLUSIONS',
      chunkIndex: 7,
      relevantText: 'We do not cover loss caused by flood, surface water, waves, tidal water, overflow of a body of water, or spray from any of these, whether or not driven by wind.',
      similarity: 0.78
    }
  ],
  llmProvider: 'Groq',
  elapsedMilliseconds: 1842
};

export const MOCK_DOCUMENT_DETAIL = {
  id: 501,
  fileName: 'homeowners-policy-2024.pdf',
  mimeType: 'application/pdf',
  category: 'Policy',
  status: 'Processed',
  pageCount: 4,
  chunkCount: 4,
  embeddingProvider: 'Voyage AI',
  chunks: [
    { chunkIndex: 0, sectionName: 'DECLARATIONS', tokenCount: 256, contentPreview: 'Named Insured: John Smith. Policy Number: HO-2024-789456. Policy Period: 01/01/2024 to 01/01/2025. Dwelling Coverage: $450,000...' },
    { chunkIndex: 1, sectionName: 'COVERAGE A - DWELLING', tokenCount: 512, contentPreview: 'We cover the dwelling on the residence premises including structures attached to the dwelling. Coverage includes sudden and accidental...' },
    { chunkIndex: 2, sectionName: 'COVERAGE B - OTHER STRUCTURES', tokenCount: 384, contentPreview: 'We cover other structures on the residence premises set apart from the dwelling by clear space. This includes detached garages...' },
    { chunkIndex: 3, sectionName: 'EXCLUSIONS', tokenCount: 448, contentPreview: 'We do not cover loss caused by: ordinance or law; earth movement; water damage from external flooding; power failure...' }
  ],
  createdAt: '2026-02-25T09:30:00Z'
};

export const MOCK_DOCUMENT_HISTORY_RESPONSE = {
  items: [
    { id: 501, fileName: 'homeowners-policy-2024.pdf', mimeType: 'application/pdf', category: 'Policy', status: 'Processed', pageCount: 4, chunkCount: 12, createdAt: '2026-02-25T09:30:00Z' },
    { id: 502, fileName: 'auto-claim-CLM-2024-001.pdf', mimeType: 'application/pdf', category: 'Claim', status: 'Processed', pageCount: 2, chunkCount: 6, createdAt: '2026-02-24T14:15:00Z' },
    { id: 503, fileName: 'endorsement-amendment-003.pdf', mimeType: 'application/pdf', category: 'Endorsement', status: 'Processed', pageCount: 1, chunkCount: 3, createdAt: '2026-02-23T11:00:00Z' }
  ],
  totalCount: 3, page: 1, pageSize: 20, totalPages: 1
};

// ===================== Customer Experience Copilot =====================

export const MOCK_CX_CHAT_RESPONSE = {
  response: 'I understand your concern about the delay in processing your water damage claim. Let me look into the current status for you. Based on the information available, your claim CLM-2024-78901 is currently in the assessment phase. A field adjuster has been assigned and should contact you within the next 24-48 hours to schedule an inspection. I apologize for the wait and want to assure you that we are working to resolve this as quickly as possible.',
  tone: 'Empathetic',
  escalationRecommended: false,
  escalationReason: null,
  llmProvider: 'Groq',
  elapsedMilliseconds: 2150,
  disclaimer: 'This response is AI-generated and does not constitute a binding commitment. Please verify all policy details with your licensed insurance agent.'
};

export const MOCK_CX_ESCALATION_RESPONSE = {
  response: 'I can see this claim has been pending for over 30 days, which is beyond our standard processing timeline. I strongly recommend escalating this to a senior claims manager for immediate review. Given the severity of the water damage and the extended delay, this requires urgent attention to prevent further property deterioration.',
  tone: 'Urgent',
  escalationRecommended: true,
  escalationReason: 'Claim processing time exceeds 30-day SLA with active property damage risk',
  llmProvider: 'Groq',
  elapsedMilliseconds: 1890,
  disclaimer: 'This response is AI-generated and does not constitute a binding commitment. Please verify all policy details with your licensed insurance agent.'
};

/** Pre-composed SSE event stream for CX streaming mock. */
export const MOCK_CX_STREAM_EVENTS = [
  'data: {"type":"content","content":"I understand ","metadata":null}\n\n',
  'data: {"type":"content","content":"your concern about ","metadata":null}\n\n',
  'data: {"type":"content","content":"the claim delay. ","metadata":null}\n\n',
  'data: {"type":"content","content":"Let me check the status ","metadata":null}\n\n',
  'data: {"type":"content","content":"for you right away.","metadata":null}\n\n',
  `data: {"type":"metadata","content":"","metadata":${JSON.stringify(MOCK_CX_CHAT_RESPONSE)}}\n\n`,
  'data: [DONE]\n\n'
].join('');

// ===================== Fraud Correlation =====================

export const MOCK_CORRELATE_RESULT = {
  claimId: 101,
  correlations: [
    {
      id: 1001,
      sourceClaimId: 101,
      correlatedClaimId: 205,
      correlationType: 'DateProximity,SharedFlags',
      correlationTypes: ['DateProximity', 'SharedFlags'],
      correlationScore: 0.78,
      details: 'Both claims filed within 15 days of each other. Shared fraud flags: timing anomaly, high claim amount. Both properties in same geographic region.',
      sourceClaimSeverity: 'High',
      sourceClaimType: 'Water Damage',
      sourceFraudScore: 0.42,
      correlatedClaimSeverity: 'High',
      correlatedClaimType: 'Water Damage',
      correlatedFraudScore: 0.65,
      detectedAt: '2026-02-25T10:30:00Z',
      status: 'Pending',
      reviewedBy: null,
      reviewedAt: null,
      dismissalReason: null
    },
    {
      id: 1002,
      sourceClaimId: 101,
      correlatedClaimId: 312,
      correlationType: 'SimilarNarrative,SameSeverity',
      correlationTypes: ['SimilarNarrative', 'SameSeverity'],
      correlationScore: 0.62,
      details: 'Narrative similarity: 0.85 (both describe burst pipe leading to extensive water damage). Same severity classification: High. Different geographic regions but same insurer.',
      sourceClaimSeverity: 'High',
      sourceClaimType: 'Water Damage',
      sourceFraudScore: 0.42,
      correlatedClaimSeverity: 'High',
      correlatedClaimType: 'Water Damage',
      correlatedFraudScore: 0.38,
      detectedAt: '2026-02-25T10:30:00Z',
      status: 'Pending',
      reviewedBy: null,
      reviewedAt: null,
      dismissalReason: null
    }
  ],
  count: 2
};

export const MOCK_CORRELATIONS_PAGINATED = {
  items: MOCK_CORRELATE_RESULT.correlations,
  totalCount: 2,
  page: 1,
  pageSize: 20,
  totalPages: 1
};
