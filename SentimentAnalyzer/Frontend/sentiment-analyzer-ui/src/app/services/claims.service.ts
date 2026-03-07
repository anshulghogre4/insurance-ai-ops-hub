import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ClaimTriageResponse,
  ClaimEvidenceResponse,
  FraudAnalysisResponse,
  ProviderHealthResponse,
  ExtendedProviderHealthResponse,
  PaginatedResponse,
  ClaimsHistoryFilter,
  BatchClaimUploadResult
} from '../models/claims.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ClaimsService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/api/insurance`;

  /** Submit a claim for AI-powered triage assessment. */
  triageClaim(text: string, interactionType: string = 'Complaint'): Observable<ClaimTriageResponse> {
    return this.http.post<ClaimTriageResponse>(`${this.apiUrl}/claims/triage`, { text, interactionType });
  }

  /** Upload multimodal evidence (image/audio/PDF) for a claim. */
  uploadEvidence(claimId: number, file: File): Observable<ClaimEvidenceResponse> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ClaimEvidenceResponse>(`${this.apiUrl}/claims/upload?claimId=${claimId}`, formData);
  }

  /** Retrieve a triaged claim by its ID. */
  getClaimById(id: number): Observable<ClaimTriageResponse> {
    return this.http.get<ClaimTriageResponse>(`${this.apiUrl}/claims/${id}`);
  }

  /** Retrieve claims history with optional filters and pagination. */
  getClaimsHistory(filters?: ClaimsHistoryFilter): Observable<PaginatedResponse<ClaimTriageResponse>> {
    let params = new HttpParams();
    if (filters?.severity) params = params.set('severity', filters.severity);
    if (filters?.status) params = params.set('status', filters.status);
    if (filters?.fromDate) params = params.set('fromDate', filters.fromDate);
    if (filters?.toDate) params = params.set('toDate', filters.toDate);
    if (filters?.pageSize) params = params.set('pageSize', filters.pageSize.toString());
    if (filters?.page) params = params.set('page', filters.page.toString());
    return this.http.get<PaginatedResponse<ClaimTriageResponse>>(`${this.apiUrl}/claims/history`, { params });
  }

  /** Run detailed fraud analysis on an existing claim. */
  analyzeFraud(claimId: number): Observable<FraudAnalysisResponse> {
    return this.http.post<FraudAnalysisResponse>(`${this.apiUrl}/fraud/analyze`, { claimId });
  }

  /** Get fraud score and risk level for a claim. */
  getFraudScore(claimId: number): Observable<FraudAnalysisResponse> {
    return this.http.get<FraudAnalysisResponse>(`${this.apiUrl}/fraud/score/${claimId}`);
  }

  /** Get claims flagged as potential fraud alerts. */
  getFraudAlerts(minScore: number = 55, pageSize: number = 50): Observable<ClaimTriageResponse[]> {
    const params = new HttpParams()
      .set('minScore', minScore.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<ClaimTriageResponse[]>(`${this.apiUrl}/fraud/alerts`, { params });
  }

  /** Get health status of all LLM providers and multimodal services. */
  getProviderHealth(): Observable<ProviderHealthResponse> {
    return this.http.get<ProviderHealthResponse>(`${this.apiUrl}/health/providers`);
  }

  /** Get extended health status of all provider chains (LLM, Embedding, OCR, NER, STT, Content Safety, Translation). */
  getExtendedProviderHealth(): Observable<ExtendedProviderHealthResponse> {
    return this.http.get<ExtendedProviderHealthResponse>(`${this.apiUrl}/health/providers/extended`);
  }

  /** Upload a CSV file for batch claim triage processing. */
  uploadBatch(file: File): Observable<BatchClaimUploadResult> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<BatchClaimUploadResult>(`${this.apiUrl}/claims/batch`, formData);
  }
}
