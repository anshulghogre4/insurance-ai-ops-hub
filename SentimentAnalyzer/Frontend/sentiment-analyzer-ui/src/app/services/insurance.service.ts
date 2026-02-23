import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  InsuranceAnalysisRequest,
  InsuranceAnalysisResponse,
  DashboardData,
  AnalysisHistoryItem
} from '../models/insurance.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class InsuranceService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/api/insurance`;

  analyzeInsurance(text: string, interactionType: string = 'General', customerId?: string): Observable<InsuranceAnalysisResponse> {
    const request: InsuranceAnalysisRequest = { text, interactionType, customerId };
    return this.http.post<InsuranceAnalysisResponse>(`${this.apiUrl}/analyze`, request);
  }

  getDashboard(): Observable<DashboardData> {
    return this.http.get<DashboardData>(`${this.apiUrl}/dashboard`);
  }

  getHistory(count: number = 20): Observable<AnalysisHistoryItem[]> {
    return this.http.get<AnalysisHistoryItem[]>(`${this.apiUrl}/history?count=${count}`);
  }

  getAnalysisById(id: number): Observable<InsuranceAnalysisResponse> {
    return this.http.get<InsuranceAnalysisResponse>(`${this.apiUrl}/${id}`);
  }
}
