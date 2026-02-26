export interface InsuranceAnalysisRequest {
  text: string;
  interactionType: string;
  customerId?: string;
}

export interface InsuranceAnalysisResponse {
  inputText?: string;
  sentiment: string;
  confidenceScore: number;
  explanation: string;
  emotionBreakdown: { [key: string]: number };
  insuranceAnalysis: InsuranceAnalysisDetail;
  quality: QualityDetail;
}

export interface InsuranceAnalysisDetail {
  purchaseIntentScore: number;
  customerPersona: string;
  journeyStage: string;
  riskIndicators: RiskIndicatorDetail;
  policyRecommendations: PolicyRecommendation[];
  interactionType: string;
  keyTopics: string[];
}

export interface RiskIndicatorDetail {
  churnRisk: string;
  complaintEscalationRisk: string;
  fraudIndicators: string;
}

export interface PolicyRecommendation {
  product: string;
  reasoning: string;
}

export interface QualityDetail {
  isValid: boolean;
  qualityScore: number;
  issues: QualityIssue[];
  suggestions: string[];
  warnings: string[];
}

export interface QualityIssue {
  severity: string;
  field: string;
  message: string;
}

// Dashboard models
export interface DashboardMetrics {
  totalAnalyses: number;
  avgPurchaseIntent: number;
  avgSentimentScore: number;
  highRiskCount: number;
}

export interface SentimentDistribution {
  positive: number;
  negative: number;
  neutral: number;
  mixed: number;
}

export interface PersonaCount {
  name: string;
  count: number;
  percentage: number;
}

export interface DashboardData {
  metrics: DashboardMetrics;
  sentimentDistribution: SentimentDistribution;
  topPersonas: PersonaCount[];
}

export interface AnalysisHistoryItem {
  id: number;
  inputTextPreview: string;
  sentiment: string;
  purchaseIntentScore: number;
  customerPersona: string;
  interactionType: string;
  churnRisk: string;
  createdAt: string;
}
