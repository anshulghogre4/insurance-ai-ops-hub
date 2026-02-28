/** Request model for submitting a claim for triage. */
export interface ClaimTriageRequest {
  text: string;
  interactionType?: string;
}

/** Full triage response from the claims pipeline. */
export interface ClaimTriageResponse {
  claimId: number;
  severity: string;
  urgency: string;
  claimType: string;
  fraudScore: number;
  fraudRiskLevel: string;
  estimatedLossRange: string;
  recommendedActions: ClaimActionResponse[];
  fraudFlags: string[];
  evidence: ClaimEvidenceResponse[];
  status: string;
  createdAt: string;
}

/** A recommended action from the triage pipeline. */
export interface ClaimActionResponse {
  action: string;
  priority: string;
  reasoning: string;
}

/** Processed multimodal evidence attached to a claim. */
export interface ClaimEvidenceResponse {
  evidenceType: string;
  provider: string;
  processedText: string;
  damageIndicators: string[];
  createdAt: string;
}

/** Fraud analysis result for a claim. */
export interface FraudAnalysisResponse {
  claimId: number;
  fraudScore: number;
  riskLevel: string;
  indicators: FraudIndicatorResponse[];
  recommendedActions: ClaimActionResponse[];
  referToSIU: boolean;
  siuReferralReason: string;
  confidence: number;
}

/** A fraud indicator detail. */
export interface FraudIndicatorResponse {
  category: string;
  description: string;
  severity: string;
}

/** Health status of all AI providers and multimodal services. */
export interface ProviderHealthResponse {
  llmProviders: LlmProviderHealth[];
  multimodalServices: ServiceHealth[];
  checkedAt: string;
}

/** Health status of a single LLM provider. */
export interface LlmProviderHealth {
  name: string;
  status: string;
  isAvailable: boolean;
  consecutiveFailures: number;
  cooldownSeconds: number;
  cooldownExpiresUtc: string | null;
}

/** Health status of a multimodal service. */
export interface ServiceHealth {
  name: string;
  isConfigured: boolean;
  status: string;
}

/** Generic pagination wrapper matching backend PaginatedResponse<T>. */
export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/** Filter parameters for claims history query. */
export interface ClaimsHistoryFilter {
  severity?: string;
  status?: string;
  fromDate?: string;
  toDate?: string;
  pageSize?: number;
  page?: number;
}

/** Result of a batch CSV claim upload operation. */
export interface BatchClaimUploadResult {
  batchId: string;
  totalCount: number;
  processedCount: number;
  successCount: number;
  errorCount: number;
  status: string;
  results: BatchClaimItemResult[];
  errors: BatchClaimError[];
}

/** Triage result for a single claim row within a batch upload. */
export interface BatchClaimItemResult {
  rowNumber: number;
  claimId: string;
  severity: string;
  fraudScore: number;
  status: string;
}

/** Validation error for a specific CSV row. */
export interface BatchClaimError {
  rowNumber: number;
  field: string;
  errorMessage: string;
}
