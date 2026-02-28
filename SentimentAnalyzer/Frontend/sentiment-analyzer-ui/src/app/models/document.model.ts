import { PaginatedResponse } from './claims.model';

// ==================== Document Intelligence RAG ====================

/** Result from uploading a document through the RAG pipeline. */
export interface DocumentUploadResult {
  documentId: number;
  fileName: string;
  status: string;
  pageCount: number;
  chunkCount: number;
  embeddingProvider: string;
  errorMessage: string | null;
}

/** SSE progress event during document upload processing. */
export interface DocumentProgressEvent {
  phase: string;
  progress: number;
  message: string;
  result: DocumentUploadResult | null;
  errorMessage: string | null;
}

/** Request body for querying documents. */
export interface DocumentQueryRequest {
  question: string;
  documentId?: number;
}

/** RAG query result with LLM answer and citations. */
export interface DocumentQueryResult {
  answer: string;
  confidence: number;
  citations: DocumentCitation[];
  llmProvider: string;
  elapsedMilliseconds: number;
  answerSafety: ContentSafetyInfo | null;
}

/** Citation pointing to a document chunk. */
export interface DocumentCitation {
  documentId: number;
  fileName: string;
  sectionName: string;
  chunkIndex: number;
  relevantText: string;
  similarity: number;
}

/** Document detail with chunk metadata. */
export interface DocumentDetailResult {
  id: number;
  fileName: string;
  mimeType: string;
  category: string;
  status: string;
  pageCount: number;
  chunkCount: number;
  embeddingProvider: string;
  chunks: ChunkSummary[];
  createdAt: string;
}

/** Chunk summary for document detail view. */
export interface ChunkSummary {
  chunkIndex: number;
  sectionName: string;
  tokenCount: number;
  contentPreview: string;
  pageNumber: number | null;
  parentChunkId: number | null;
  chunkLevel: number;
  isSafe: boolean;
  safetyFlags: string | null;
}

/** Content safety screening result. */
export interface ContentSafetyInfo {
  isSafe: boolean;
  flaggedCategories: string[];
  provider: string;
}

/** Document list summary (for history). */
export interface DocumentSummary {
  id: number;
  fileName: string;
  mimeType: string;
  category: string;
  status: string;
  pageCount: number;
  chunkCount: number;
  createdAt: string;
}

/** Valid document categories. */
export type DocumentCategory = 'Policy' | 'Claim' | 'Endorsement' | 'Correspondence' | 'Other';

/** Filter params for document history query. */
export interface DocumentHistoryFilter {
  category?: DocumentCategory;
  pageSize?: number;
  page?: number;
}

// ==================== Fine-Tuning Preparation (Synthetic Q&A) ====================

/** Result from generating synthetic Q&A pairs for fine-tuning. */
export interface SyntheticQAResult {
  documentId: number;
  documentName: string;
  totalPairsGenerated: number;
  pairs: QAPair[];
  llmProvider: string;
  elapsedMilliseconds: number;
  errorMessage: string | null;
}

/** A single question-answer pair generated from a document chunk. */
export interface QAPair {
  id: number;
  chunkId: number;
  question: string;
  answer: string;
  category: string;
  confidence: number;
  sectionName: string;
}

// ==================== Customer Experience Copilot ====================

/** Request body for CX Copilot chat. */
export interface CustomerExperienceRequest {
  message: string;
  claimContext?: string;
  sessionId?: string;
}

/** Response from creating a new CX conversation session. */
export interface CxSessionResponse {
  sessionId: string;
}

/** Response containing the full message history for a conversation session. */
export interface CxMessageHistoryResponse {
  sessionId: string;
  messages: CxMessageRecord[];
}

/** A single message in a CX Copilot conversation history. */
export interface CxMessageRecord {
  role: string;
  content: string;
  timestamp: string;
}

/** Non-streaming CX Copilot response. */
export interface CustomerExperienceResponse {
  response: string;
  tone: string;
  escalationRecommended: boolean;
  escalationReason: string | null;
  llmProvider: string;
  elapsedMilliseconds: number;
  disclaimer: string | null;
}

/** SSE stream chunk from CX Copilot. */
export interface CustomerExperienceStreamChunk {
  type: string;
  content: string;
  metadata: CustomerExperienceResponse | null;
}

/** Local chat message for UI history. */
export interface ChatMessage {
  id?: number;
  role: 'user' | 'assistant';
  content: string;
  tone?: string;
  escalationRecommended?: boolean;
  escalationReason?: string | null;
  llmProvider?: string;
  elapsedMs?: number;
  disclaimer?: string | null;
  timestamp: Date;
}

// ==================== Fraud Correlation ====================

/** Fraud correlation between two claims. */
export interface FraudCorrelationResponse {
  id: number;
  sourceClaimId: number;
  correlatedClaimId: number;
  correlationType: string;
  correlationTypes: string[];
  correlationScore: number;
  details: string;
  sourceClaimSeverity: string | null;
  sourceClaimType: string | null;
  sourceFraudScore: number | null;
  correlatedClaimSeverity: string | null;
  correlatedClaimType: string | null;
  correlatedFraudScore: number | null;
  detectedAt: string;
  status: string;
  reviewedBy: string | null;
  reviewedAt: string | null;
  dismissalReason: string | null;
}

/** Request body for reviewing a correlation. */
export interface ReviewCorrelationRequest {
  status: 'Pending' | 'Confirmed' | 'Dismissed';
  reviewedBy?: string;
  dismissalReason?: string;
}

/** Response from POST /fraud/correlate. */
export interface CorrelateResult {
  claimId: number;
  correlations: FraudCorrelationResponse[];
  count: number;
}

/** Re-export PaginatedResponse for convenience. */
export type { PaginatedResponse };
