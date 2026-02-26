import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DocumentService } from './document.service';
import {
  DocumentUploadResult,
  DocumentQueryResult,
  DocumentDetailResult,
  DocumentSummary
} from '../models/document.model';
import { PaginatedResponse } from '../models/claims.model';

describe('DocumentService', () => {
  let service: DocumentService;
  let httpMock: HttpTestingController;

  const baseUrl = 'http://localhost:5143/api/insurance/documents';

  const mockUploadResult: DocumentUploadResult = {
    documentId: 101,
    fileName: 'homeowners-policy-2026.pdf',
    status: 'Processed',
    pageCount: 12,
    chunkCount: 38,
    embeddingProvider: 'Groq',
    errorMessage: null
  };

  const mockQueryResult: DocumentQueryResult = {
    answer: 'The deductible for wind damage under this homeowners policy is $2,500 per occurrence.',
    confidence: 0.91,
    citations: [
      {
        documentId: 101,
        fileName: 'homeowners-policy-2026.pdf',
        sectionName: 'Section IV - Deductibles',
        chunkIndex: 14,
        relevantText: 'Wind and hail damage deductible: $2,500 per occurrence applies to all covered structures.',
        similarity: 0.94
      }
    ],
    llmProvider: 'Groq',
    elapsedMilliseconds: 1245
  };

  const mockDocumentDetail: DocumentDetailResult = {
    id: 101,
    fileName: 'homeowners-policy-2026.pdf',
    mimeType: 'application/pdf',
    category: 'Policy',
    status: 'Processed',
    pageCount: 12,
    chunkCount: 38,
    embeddingProvider: 'Groq',
    chunks: [
      {
        chunkIndex: 0,
        sectionName: 'Declarations Page',
        tokenCount: 312,
        contentPreview: 'Policy Number: HO-2026-4491827 | Named Insured: Sarah Mitchell | Property Address: 1422 Oakwood Dr...'
      },
      {
        chunkIndex: 1,
        sectionName: 'Section I - Coverages',
        tokenCount: 487,
        contentPreview: 'Coverage A - Dwelling: $425,000 | Coverage B - Other Structures: $42,500...'
      }
    ],
    createdAt: '2026-02-25T14:30:00Z'
  };

  const mockDocumentHistory: PaginatedResponse<DocumentSummary> = {
    items: [
      {
        id: 101,
        fileName: 'homeowners-policy-2026.pdf',
        mimeType: 'application/pdf',
        category: 'Policy',
        status: 'Processed',
        pageCount: 12,
        chunkCount: 38,
        createdAt: '2026-02-25T14:30:00Z'
      },
      {
        id: 102,
        fileName: 'auto-claim-denial-letter.pdf',
        mimeType: 'application/pdf',
        category: 'Correspondence',
        status: 'Processed',
        pageCount: 3,
        chunkCount: 9,
        createdAt: '2026-02-24T09:15:00Z'
      }
    ],
    totalCount: 2,
    page: 1,
    pageSize: 20,
    totalPages: 1
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        DocumentService
      ]
    });
    service = TestBed.inject(DocumentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should upload document with FormData and category param', () => {
    const file = new File(['policy content'], 'commercial-liability-endorsement.pdf', { type: 'application/pdf' });

    service.uploadDocument(file, 'Endorsement').subscribe(res => {
      expect(res.documentId).toBe(101);
      expect(res.fileName).toBe('homeowners-policy-2026.pdf');
      expect(res.status).toBe('Processed');
      expect(res.chunkCount).toBe(38);
    });

    const req = httpMock.expectOne(r => r.url === `${baseUrl}/upload`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBe(true);
    expect(req.request.params.get('category')).toBe('Endorsement');
    req.flush(mockUploadResult);
  });

  it('should query documents with question', () => {
    service.queryDocuments('What is the wind damage deductible?', 101).subscribe(res => {
      expect(res.answer).toContain('$2,500');
      expect(res.confidence).toBe(0.91);
      expect(res.citations.length).toBe(1);
      expect(res.citations[0].sectionName).toBe('Section IV - Deductibles');
      expect(res.llmProvider).toBe('Groq');
    });

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ question: 'What is the wind damage deductible?', documentId: 101 });
    req.flush(mockQueryResult);
  });

  it('should get document by ID', () => {
    service.getDocumentById(101).subscribe(res => {
      expect(res.id).toBe(101);
      expect(res.fileName).toBe('homeowners-policy-2026.pdf');
      expect(res.category).toBe('Policy');
      expect(res.chunks.length).toBe(2);
      expect(res.chunks[0].sectionName).toBe('Declarations Page');
    });

    const req = httpMock.expectOne(`${baseUrl}/101`);
    expect(req.request.method).toBe('GET');
    req.flush(mockDocumentDetail);
  });

  it('should get document history with filters', () => {
    service.getDocumentHistory({ category: 'Policy', page: 1, pageSize: 10 }).subscribe(res => {
      expect(res.items.length).toBe(2);
      expect(res.totalCount).toBe(2);
      expect(res.items[0].category).toBe('Policy');
    });

    const req = httpMock.expectOne(r => r.url === `${baseUrl}/history`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('category')).toBe('Policy');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('10');
    req.flush(mockDocumentHistory);
  });

  it('should get document history without filters', () => {
    const emptyHistory: PaginatedResponse<DocumentSummary> = {
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0
    };

    service.getDocumentHistory().subscribe(res => {
      expect(res.items.length).toBe(0);
      expect(res.totalCount).toBe(0);
    });

    const req = httpMock.expectOne(`${baseUrl}/history`);
    expect(req.request.method).toBe('GET');
    req.flush(emptyHistory);
  });

  it('should delete document by ID', () => {
    service.deleteDocument(101).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/101`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
