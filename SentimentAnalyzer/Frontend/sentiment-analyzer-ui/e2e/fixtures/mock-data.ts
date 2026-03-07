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
  fraudScore: 48,
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
    { claimId: 101, severity: 'High', urgency: 'Immediate', claimType: 'Water Damage', fraudScore: 48, fraudRiskLevel: 'Medium', estimatedLossRange: '$5K-$15K', recommendedActions: [], fraudFlags: [], evidence: [], status: 'Triaged', createdAt: '2026-02-24T10:00:00Z' },
    { claimId: 102, severity: 'Low', urgency: 'Standard', claimType: 'Auto Scratch', fraudScore: 12, fraudRiskLevel: 'Low', estimatedLossRange: '$500-$1,500', recommendedActions: [], fraudFlags: [], evidence: [], status: 'Resolved', createdAt: '2026-02-23T14:00:00Z' },
    { claimId: 103, severity: 'Critical', urgency: 'Emergency', claimType: 'Structure Fire', fraudScore: 78, fraudRiskLevel: 'High', estimatedLossRange: '$50K-$200K', recommendedActions: [], fraudFlags: ['Timing anomaly', 'Financial motive'], evidence: [], status: 'UnderReview', createdAt: '2026-02-22T08:00:00Z' }
  ],
  totalCount: 3, page: 1, pageSize: 20, totalPages: 1
};

export const MOCK_FRAUD_ALERTS_RESPONSE = [
  { claimId: 201, severity: 'Critical', urgency: 'Emergency', claimType: 'Structure Fire', fraudScore: 92, fraudRiskLevel: 'VeryHigh', estimatedLossRange: '$100K-$500K', recommendedActions: [{ action: 'Refer to SIU', priority: 'Critical', reasoning: 'Multiple fraud indicators' }], fraudFlags: ['Timing anomaly', 'Financial motive', 'Inconsistent documentation'], evidence: [], status: 'UnderReview', createdAt: '2026-02-24T08:00:00Z' },
  { claimId: 202, severity: 'High', urgency: 'Priority', claimType: 'Theft', fraudScore: 72, fraudRiskLevel: 'High', estimatedLossRange: '$10K-$25K', recommendedActions: [], fraudFlags: ['Pattern match with known fraud ring'], evidence: [], status: 'UnderReview', createdAt: '2026-02-23T14:00:00Z' }
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

export const MOCK_EXTENDED_PROVIDER_HEALTH = {
  llmProviders: [
    { name: 'Groq', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
    { name: 'Cerebras', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
    { name: 'Mistral', status: 'Degraded', isAvailable: true, consecutiveFailures: 2, cooldownSeconds: 0, cooldownExpiresUtc: null },
    { name: 'Gemini', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
    { name: 'OpenRouter', status: 'Down', isAvailable: false, consecutiveFailures: 5, cooldownSeconds: 120, cooldownExpiresUtc: '2026-02-24T10:05:00Z' },
    { name: 'OpenAI', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null },
    { name: 'Ollama', status: 'Healthy', isAvailable: true, consecutiveFailures: 0, cooldownSeconds: 0, cooldownExpiresUtc: null }
  ],
  embeddingProviders: [
    { name: 'Voyage AI', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 1, freeTierLimit: '50M tokens' },
    { name: 'Cohere', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 2, freeTierLimit: '100 req/min' },
    { name: 'Gemini', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 3, freeTierLimit: '1,500 req/day' },
    { name: 'HuggingFace', status: 'NotConfigured', isConfigured: false, isAvailable: false, chainOrder: 4, freeTierLimit: '300 req/hr' },
    { name: 'Jina', status: 'NotConfigured', isConfigured: false, isAvailable: false, chainOrder: 5, freeTierLimit: '1M tokens' },
    { name: 'Ollama (Local)', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 6, freeTierLimit: 'Unlimited (local)' }
  ],
  ocrProviders: [
    { name: 'PdfPig (Local)', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 1, freeTierLimit: 'Unlimited (local)' },
    { name: 'Tesseract (Local)', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 2, freeTierLimit: 'Unlimited (local)' },
    { name: 'Azure Document Intelligence', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 3, freeTierLimit: '500 pages/month' },
    { name: 'Mistral OCR', status: 'NotConfigured', isConfigured: false, isAvailable: false, chainOrder: 4, freeTierLimit: '1B tokens/month' },
    { name: 'OCR Space', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 5, freeTierLimit: '500 req/day' },
    { name: 'Gemini Vision', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 6, freeTierLimit: '1,500 req/day' }
  ],
  nerProviders: [
    { name: 'HuggingFace BERT', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 1, freeTierLimit: '300 req/hr' },
    { name: 'Azure AI Language', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 2, freeTierLimit: '5K/month' }
  ],
  sttProviders: [
    { name: 'Deepgram', status: 'Healthy', isConfigured: true, isAvailable: true, chainOrder: 1, freeTierLimit: '$200 credit' },
    { name: 'Azure AI Speech', status: 'NotConfigured', isConfigured: false, isAvailable: false, chainOrder: 2, freeTierLimit: '5 hrs/month' }
  ],
  contentSafety: [
    { name: 'Azure AI Content Safety', isConfigured: true, status: 'Available' }
  ],
  translation: [
    { name: 'Azure AI Translator', isConfigured: true, status: 'Available' }
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
  pageCount: 18,
  chunkCount: 42,
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
  elapsedMilliseconds: 1842,
  answerSafety: null,
  queryReformulated: false,
  answerQualityScore: 0.92,
  isGrounded: true,
  crossDocConflicts: []
};

export const MOCK_DOCUMENT_DETAIL = {
  id: 501,
  fileName: 'homeowners-policy-2024.pdf',
  mimeType: 'application/pdf',
  category: 'Policy',
  status: 'Processed',
  pageCount: 18,
  chunkCount: 7,
  embeddingProvider: 'Voyage AI',
  chunks: [
    { chunkIndex: 0, sectionName: 'DECLARATIONS', tokenCount: 256, contentPreview: 'Named Insured: Robert Thompson. Policy Number: HO-2024-78432. Property Address: 742 Evergreen Terrace...', pageNumber: 1, parentChunkId: null, chunkLevel: 0, isSafe: true, safetyFlags: null },
    { chunkIndex: 1, sectionName: 'COVERAGE', tokenCount: 512, contentPreview: 'Coverage A - Dwelling: Your policy covers the dwelling on the residence premises shown in the declarations...', pageNumber: 2, parentChunkId: null, chunkLevel: 0, isSafe: true, safetyFlags: null },
    { chunkIndex: 2, sectionName: 'COVERAGE', tokenCount: 384, contentPreview: 'Coverage B - Other Structures: This coverage applies to other structures on the residence premises...', pageNumber: 3, parentChunkId: 1, chunkLevel: 1, isSafe: true, safetyFlags: null },
    { chunkIndex: 3, sectionName: 'EXCLUSIONS', tokenCount: 448, contentPreview: 'We do not insure for loss caused directly or indirectly by: flood, surface water, waves, tidal water...', pageNumber: 5, parentChunkId: null, chunkLevel: 0, isSafe: true, safetyFlags: null },
    { chunkIndex: 4, sectionName: 'SUBROGATION', tokenCount: 320, contentPreview: 'If an insured has rights to recover from a responsible party for a loss we have paid, those rights...', pageNumber: 12, parentChunkId: null, chunkLevel: 0, isSafe: true, safetyFlags: null },
    { chunkIndex: 5, sectionName: 'CANCELLATION', tokenCount: 280, contentPreview: 'Either the named insured or we may cancel this policy. Cancellation by us requires 30 days written...', pageNumber: 15, parentChunkId: null, chunkLevel: 0, isSafe: true, safetyFlags: null },
    { chunkIndex: 6, sectionName: 'DISPUTE RESOLUTION', tokenCount: 350, contentPreview: 'In the event of a dispute regarding claim settlement amounts, the insured may invoke the appraisal process...', pageNumber: 16, parentChunkId: null, chunkLevel: 0, isSafe: false, safetyFlags: 'Violence|SelfHarm' }
  ],
  createdAt: '2026-02-25T09:30:00Z'
};

export const MOCK_DOCUMENT_HISTORY_RESPONSE = {
  items: [
    { id: 501, fileName: 'homeowners-policy-2024.pdf', mimeType: 'application/pdf', category: 'Policy', status: 'Processed', pageCount: 18, chunkCount: 42, createdAt: '2026-02-25T09:30:00Z' },
    { id: 502, fileName: 'auto-claim-CLM-2024-001.pdf', mimeType: 'application/pdf', category: 'Claim', status: 'Processed', pageCount: 2, chunkCount: 6, createdAt: '2026-02-24T14:15:00Z' },
    { id: 503, fileName: 'endorsement-amendment-003.pdf', mimeType: 'application/pdf', category: 'Endorsement', status: 'Processed', pageCount: 1, chunkCount: 3, createdAt: '2026-02-23T11:00:00Z' },
    { id: 504, fileName: 'adjuster-correspondence.png', mimeType: 'image/png', category: 'Correspondence', status: 'Processed', pageCount: 1, chunkCount: 2, createdAt: '2026-02-22T16:45:00Z' }
  ],
  totalCount: 4, page: 1, pageSize: 12, totalPages: 1
};

/** Pre-composed SSE event stream for document upload progress mock. */
export const MOCK_UPLOAD_PROGRESS_SSE = [
  'data: {"phase":"Uploading","progress":5,"message":"Receiving homeowners-policy-2024.pdf (245 KB)...","result":null,"errorMessage":null}\n\n',
  'data: {"phase":"Uploading","progress":10,"message":"Document registered. Starting OCR extraction...","result":null,"errorMessage":null}\n\n',
  'data: {"phase":"OCR","progress":15,"message":"Extracting text with OCR...","result":null,"errorMessage":null}\n\n',
  'data: {"phase":"OCR","progress":30,"message":"Extracted text from 18 pages.","result":null,"errorMessage":null}\n\n',
  'data: {"phase":"Chunking","progress":35,"message":"Splitting into insurance sections...","result":null,"errorMessage":null}\n\n',
  'data: {"phase":"Chunking","progress":45,"message":"Created 42 chunks (4 sections, 12 sub-chunks).","result":null,"errorMessage":null}\n\n',
  'data: {"phase":"Embedding","progress":50,"message":"Generating vector embeddings for 42 chunks...","result":null,"errorMessage":null}\n\n',
  'data: {"phase":"Embedding","progress":75,"message":"Embeddings generated via Voyage AI (1024-dim).","result":null,"errorMessage":null}\n\n',
  'data: {"phase":"Safety","progress":80,"message":"Storing document index...","result":null,"errorMessage":null}\n\n',
  'data: {"phase":"Done","progress":100,"message":"Document ready for queries.","result":' + JSON.stringify({
    documentId: 501,
    fileName: 'homeowners-policy-2024.pdf',
    status: 'Processed',
    pageCount: 18,
    chunkCount: 42,
    embeddingProvider: 'Voyage AI',
    errorMessage: null
  }) + ',"errorMessage":null}\n\n',
  'data: [DONE]\n\n'
].join('');

// ===================== Customer Experience Copilot =====================

export const MOCK_CX_CHAT_RESPONSE = {
  response: 'I understand your concern about the delay in processing your water damage claim. Let me look into the current status for you. Based on the information available, your claim CLM-2024-78901 is currently in the assessment phase. A field adjuster has been assigned and should contact you within the next 24-48 hours to schedule an inspection. I apologize for the wait and want to assure you that we are working to resolve this as quickly as possible.',
  tone: 'Empathetic',
  escalationRecommended: false,
  escalationReason: null,
  llmProvider: 'Groq',
  elapsedMilliseconds: 2150,
  disclaimer: 'This response is AI-generated and does not constitute a binding commitment. Please verify all policy details with your licensed insurance agent.',
  contentSafetyScreened: true
};

export const MOCK_CX_ESCALATION_RESPONSE = {
  response: 'I can see this claim has been pending for over 30 days, which is beyond our standard processing timeline. I strongly recommend escalating this to a senior claims manager for immediate review. Given the severity of the water damage and the extended delay, this requires urgent attention to prevent further property deterioration.',
  tone: 'Urgent',
  escalationRecommended: true,
  escalationReason: 'Claim processing time exceeds 30-day SLA with active property damage risk',
  llmProvider: 'Groq',
  elapsedMilliseconds: 1890,
  disclaimer: 'This response is AI-generated and does not constitute a binding commitment. Please verify all policy details with your licensed insurance agent.',
  contentSafetyScreened: true
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

// ===================== CX Copilot Conversation Memory =====================

export const MOCK_CX_SESSION_RESPONSE = {
  sessionId: 'e2e-session-abc-12345678-90ab-cdef'
};

export const MOCK_CX_SESSION_HISTORY_RESPONSE = {
  sessionId: 'e2e-session-abc-12345678-90ab-cdef',
  messages: [
    {
      role: 'user',
      content: 'What does my homeowners policy cover for water damage?',
      timestamp: '2026-02-28T10:00:00Z'
    },
    {
      role: 'assistant',
      content: 'Your homeowners policy covers sudden and accidental water damage from burst pipes and appliance overflow. Gradual damage and external flooding require separate flood insurance. Your deductible is $1,000 per occurrence.',
      timestamp: '2026-02-28T10:00:05Z'
    },
    {
      role: 'user',
      content: 'How do I file a water damage claim?',
      timestamp: '2026-02-28T10:01:00Z'
    },
    {
      role: 'assistant',
      content: 'To file a water damage claim, call our claims hotline at the number on your policy declarations page. Have your policy number ready. An adjuster will be assigned within 24-48 hours to inspect the damage.',
      timestamp: '2026-02-28T10:01:04Z'
    }
  ]
};

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
      sourceFraudScore: 42,
      correlatedClaimSeverity: 'High',
      correlatedClaimType: 'Water Damage',
      correlatedFraudScore: 65,
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
      sourceFraudScore: 42,
      correlatedClaimSeverity: 'High',
      correlatedClaimType: 'Water Damage',
      correlatedFraudScore: 38,
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

// ===================== Fine-Tuning Synthetic Q&A =====================

export const MOCK_QA_PAIRS = {
  documentId: 1,
  documentName: 'auto-insurance-policy.pdf',
  totalPairsGenerated: 6,
  pairs: [
    {
      id: 1,
      chunkId: 1,
      question: 'What is the deductible for comprehensive coverage under this auto insurance policy?',
      answer: 'The comprehensive coverage deductible is $500 per occurrence, as specified in Section 4.2 of the policy declarations page.',
      category: 'factual',
      confidence: 0.95,
      sectionName: 'Coverage Details'
    },
    {
      id: 2,
      chunkId: 1,
      question: 'How would a total loss claim be processed under this policy?',
      answer: 'A total loss claim would trigger the actual cash value provision, where the insurer pays the market value of the vehicle minus the deductible, based on the valuation method described in Section 7.',
      category: 'inferential',
      confidence: 0.88,
      sectionName: 'Coverage Details'
    },
    {
      id: 3,
      chunkId: 2,
      question: 'What steps should a policyholder take to file a claim after an accident?',
      answer: 'The policyholder must: 1) Report the incident within 72 hours via the claims hotline, 2) Provide a police report number, 3) Submit photos of damage, 4) Complete the sworn proof of loss form within 30 days.',
      category: 'procedural',
      confidence: 0.92,
      sectionName: 'Claims Procedure'
    },
    {
      id: 4,
      chunkId: 3,
      question: 'What are the conditions for policy cancellation by the insurer?',
      answer: 'The insurer may cancel the policy for: non-payment of premium (10-day notice), material misrepresentation on the application (30-day notice), or substantial increase in hazard (30-day notice per state regulations).',
      category: 'factual',
      confidence: 0.91,
      sectionName: 'Cancellation and Nonrenewal'
    },
    {
      id: 5,
      chunkId: 4,
      question: 'Under what circumstances would subrogation rights apply?',
      answer: 'Subrogation rights apply when a third party is at fault for the loss. The insurer, after paying the claim, assumes the policyholder\'s right to recover damages from the responsible party.',
      category: 'inferential',
      confidence: 0.87,
      sectionName: 'Subrogation'
    },
    {
      id: 6,
      chunkId: 4,
      question: 'How should the insured cooperate during a subrogation recovery?',
      answer: 'The insured must: 1) Not settle with the at-fault party without insurer consent, 2) Provide all requested documentation, 3) Testify if required, 4) Assign recovery rights to the insurer as specified in the policy terms.',
      category: 'procedural',
      confidence: 0.89,
      sectionName: 'Subrogation'
    }
  ],
  llmProvider: 'Groq',
  elapsedMilliseconds: 4523,
  errorMessage: null
};

// ===================== Batch Claims CSV Upload =====================

export const MOCK_BATCH_CLAIM_UPLOAD_RESULT = {
  batchId: 'BATCH-20260228-A1B2C3D4',
  totalCount: 7,
  processedCount: 7,
  successCount: 5,
  errorCount: 2,
  status: 'Completed',
  results: [
    { rowNumber: 2, claimId: 'CLM-2024-001', severity: 'High', fraudScore: 42, status: 'Triaged' },
    { rowNumber: 3, claimId: 'CLM-2024-002', severity: 'Medium', fraudScore: 25, status: 'Triaged' },
    { rowNumber: 4, claimId: 'CLM-2024-003', severity: 'Critical', fraudScore: 78, status: 'Triaged' },
    { rowNumber: 5, claimId: 'CLM-2024-004', severity: 'Low', fraudScore: 12, status: 'Triaged' },
    { rowNumber: 8, claimId: 'CLM-2024-007', severity: 'High', fraudScore: 65, status: 'Triaged' }
  ],
  errors: [
    { rowNumber: 6, field: 'ClaimId', errorMessage: 'ClaimId is required and cannot be empty.' },
    { rowNumber: 7, field: 'EstimatedAmount', errorMessage: "EstimatedAmount 'not-a-number' is not a valid positive number." }
  ]
};
