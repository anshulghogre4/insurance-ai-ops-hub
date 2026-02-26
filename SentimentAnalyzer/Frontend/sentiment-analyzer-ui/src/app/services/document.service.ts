import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DocumentUploadResult,
  DocumentQueryResult,
  DocumentDetailResult,
  DocumentSummary,
  DocumentCategory,
  DocumentHistoryFilter
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
}
