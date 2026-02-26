import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CustomerExperienceResponse, CustomerExperienceStreamChunk } from '../models/document.model';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class CustomerExperienceService {
  private http = inject(HttpClient);
  private authService = inject(AuthService);
  private apiUrl = `${environment.apiUrl}/api/insurance/cx`;

  /** Non-streaming CX Copilot chat. */
  chat(message: string, claimContext?: string): Observable<CustomerExperienceResponse> {
    const body: { message: string; claimContext?: string } = { message };
    if (claimContext) body.claimContext = claimContext;
    return this.http.post<CustomerExperienceResponse>(`${this.apiUrl}/chat`, body);
  }

  /**
   * SSE streaming CX Copilot chat.
   * Uses fetch() + ReadableStream because Angular HttpClient doesn't support SSE from POST.
   * Emits CustomerExperienceStreamChunk for each SSE event, completes on [DONE].
   */
  streamChat(message: string, claimContext?: string): Observable<CustomerExperienceStreamChunk> {
    return new Observable(subscriber => {
      const controller = new AbortController();
      const body: { message: string; claimContext?: string } = { message };
      if (claimContext) body.claimContext = claimContext;

      const headers: Record<string, string> = { 'Content-Type': 'application/json' };
      const token = this.authService.session()?.access_token;
      if (token) headers['Authorization'] = `Bearer ${token}`;

      fetch(`${this.apiUrl}/stream`, {
        method: 'POST',
        headers,
        body: JSON.stringify(body),
        signal: controller.signal
      }).then(async response => {
        if (!response.ok) {
          subscriber.error(new Error(`HTTP ${response.status}: ${response.statusText}`));
          return;
        }
        if (!response.body) {
          subscriber.error(new Error('Response body is empty'));
          return;
        }
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split('\n');
          buffer = lines.pop()!;

          for (const line of lines) {
            const trimmed = line.trim();
            if (!trimmed || !trimmed.startsWith('data: ')) continue;

            const data = trimmed.slice(6);
            if (data === '[DONE]') {
              subscriber.complete();
              return;
            }

            try {
              const chunk: CustomerExperienceStreamChunk = JSON.parse(data);
              subscriber.next(chunk);
            } catch {
              // Skip malformed lines
            }
          }
        }
        subscriber.complete();
      }).catch(err => {
        if (err.name !== 'AbortError') {
          subscriber.error(err);
        }
      });

      return () => controller.abort();
    });
  }
}
