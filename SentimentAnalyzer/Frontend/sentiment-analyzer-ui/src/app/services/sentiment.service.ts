import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { SentimentRequest, SentimentResponse } from '../models/sentiment.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class SentimentService {
  private apiUrl = `${environment.apiUrl}/api/sentiment`;

  constructor(private http: HttpClient) { }

  analyzeSentiment(text: string): Observable<SentimentResponse> {
    const request: SentimentRequest = { text };
    return this.http.post<SentimentResponse>(`${this.apiUrl}/analyze`, request);
  }
}
