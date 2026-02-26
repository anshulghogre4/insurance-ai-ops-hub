import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CorrelateResult, FraudCorrelationResponse, ReviewCorrelationRequest } from '../models/document.model';
import { PaginatedResponse } from '../models/claims.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class FraudCorrelationService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/api/insurance/fraud`;

  /** Trigger cross-claim fraud correlation analysis. */
  correlate(claimId: number): Observable<CorrelateResult> {
    return this.http.post<CorrelateResult>(`${this.apiUrl}/correlate`, { claimId });
  }

  /** Get paginated correlations for a specific claim. */
  getCorrelations(claimId: number, page: number = 1, pageSize: number = 20): Observable<PaginatedResponse<FraudCorrelationResponse>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PaginatedResponse<FraudCorrelationResponse>>(`${this.apiUrl}/correlations/${claimId}`, { params });
  }

  /** Review (confirm/dismiss) a fraud correlation. */
  reviewCorrelation(id: number, request: ReviewCorrelationRequest): Observable<{ id: number; status: string; message: string }> {
    return this.http.patch<{ id: number; status: string; message: string }>(`${this.apiUrl}/correlations/${id}/review`, request);
  }

  /** Delete all correlations for a claim. */
  deleteCorrelations(claimId: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/correlations/${claimId}`);
  }
}
