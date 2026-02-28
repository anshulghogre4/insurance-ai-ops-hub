import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DocumentUploadResult,
  DocumentQueryResult,
  DocumentDetailResult,
  DocumentSummary,
  DocumentCategory,
  DocumentHistoryFilter,
  DocumentProgressEvent,
  SyntheticQAResult
} from '../models/document.model';
import { PaginatedResponse } from '../models/claims.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class DocumentService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/api/insurance/documents`;

  /** Upload a document for RAG processing (OCR → chunk → embed → store). */
  uploadDocument(file: File, category: DocumentCategory = 'Other'): Observable<DocumentUploadResult> {
    const formData = new FormData();
    formData.append('file', file);
    const params = new HttpParams().set('category', category);
    return this.http.post<DocumentUploadResult>(`${this.apiUrl}/upload`, formData, { params });
  }

  /** Query documents using natural language (RAG pipeline). */
  queryDocuments(question: string, documentId?: number): Observable<DocumentQueryResult> {
    const body: { question: string; documentId?: number } = { question };
    if (documentId != null) body.documentId = documentId;
    return this.http.post<DocumentQueryResult>(`${this.apiUrl}/query`, body);
  }

  /** Get document detail by ID including chunk metadata. */
  getDocumentById(id: number): Observable<DocumentDetailResult> {
    return this.http.get<DocumentDetailResult>(`${this.apiUrl}/${id}`);
  }

  /** Get document history with optional filters and pagination. */
  getDocumentHistory(filters?: DocumentHistoryFilter): Observable<PaginatedResponse<DocumentSummary>> {
    let params = new HttpParams();
    if (filters?.category) params = params.set('category', filters.category);
    if (filters?.pageSize) params = params.set('pageSize', filters.pageSize.toString());
    if (filters?.page) params = params.set('page', filters.page.toString());
    return this.http.get<PaginatedResponse<DocumentSummary>>(`${this.apiUrl}/history`, { params });
  }

  /** Delete a document and all its chunks/embeddings. */
  deleteDocument(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /** Generate synthetic Q&A pairs from a processed document for fine-tuning. */
  generateQAPairs(documentId: number): Observable<SyntheticQAResult> {
    return this.http.post<SyntheticQAResult>(
      `${this.apiUrl}/${documentId}/generate-qa`, {}
    );
  }

  /** Retrieve previously generated Q&A pairs for a document. */
  getQAPairs(documentId: number): Observable<SyntheticQAResult> {
    return this.http.get<SyntheticQAResult>(
      `${this.apiUrl}/${documentId}/qa-pairs`
    );
  }

  /**
   * Upload document with real-time SSE progress streaming.
   * Uses fetch() + ReadableStream because Angular HttpClient doesn't support SSE from POST.
   * Emits DocumentProgressEvent for each SSE event, completes on [DONE].
   */
  uploadDocumentWithProgress(file: File, category: DocumentCategory = 'Other'): Observable<DocumentProgressEvent> {
    return new Observable(subscriber => {
      const controller = new AbortController();
      const formData = new FormData();
      formData.append('file', file);

      fetch(`${environment.apiUrl}/api/insurance/documents/upload/stream?category=${encodeURIComponent(category)}`, {
        method: 'POST',
        body: formData,
        signal: controller.signal
      }).then(async response => {
        if (!response.ok) {
          const errorText = await response.text();
          subscriber.error(new Error(errorText || `Upload failed: ${response.status}`));
          return;
        }

        const reader = response.body?.getReader();
        if (!reader) {
          subscriber.error(new Error('No response body'));
          return;
        }

        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split('\n');
          buffer = lines.pop() ?? '';

          for (const line of lines) {
            const trimmed = line.trim();
            if (!trimmed || !trimmed.startsWith('data: ')) continue;
            const data = trimmed.slice(6);
            if (data === '[DONE]') {
              subscriber.complete();
              return;
            }
            try {
              const event: DocumentProgressEvent = JSON.parse(data);
              subscriber.next(event);
            } catch {
              // Skip malformed SSE lines
            }
          }
        }
        // Process remaining buffer after stream ends
        if (buffer.trim()) {
          const trimmed = buffer.trim();
          if (trimmed.startsWith('data: ')) {
            const data = trimmed.slice(6);
            if (data !== '[DONE]') {
              try {
                const event: DocumentProgressEvent = JSON.parse(data);
                subscriber.next(event);
              } catch { /* skip malformed */ }
            }
          }
        }
        subscriber.complete();
      }).catch(err => {
        if (err.name !== 'AbortError') {
          subscriber.error(new Error('Connection lost during document processing. Please try again.'));
        }
      });

      return () => controller.abort();
    });
  }
}
