import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { of, Subject } from 'rxjs';
import { DocumentResultComponent } from './document-result';
import { DocumentService } from '../../services/document.service';
import { DocumentDetailResult } from '../../models/document.model';

describe('DocumentResultComponent', () => {
  let fixture: ComponentFixture<DocumentResultComponent>;
  let component: DocumentResultComponent;

  const mockDocumentDetail: DocumentDetailResult = {
    id: 501,
    fileName: 'workers-comp-claim-WC-2026-33012.pdf',
    mimeType: 'application/pdf',
    category: 'Claim',
    status: 'Processed',
    pageCount: 6,
    chunkCount: 18,
    embeddingProvider: 'Groq',
    chunks: [
      {
        chunkIndex: 0,
        sectionName: 'Incident Report',
        tokenCount: 285,
        contentPreview: 'Employee John Rivera sustained a lower back injury on 2026-01-15 while lifting a 50lb equipment crate at the warehouse...'
      },
      {
        chunkIndex: 1,
        sectionName: 'Medical Evaluation',
        tokenCount: 410,
        contentPreview: 'Diagnosis: L4-L5 disc herniation confirmed via MRI. Treatment plan includes physical therapy 3x/week and epidural injection...'
      },
      {
        chunkIndex: 2,
        sectionName: 'Employer Statement',
        tokenCount: 198,
        contentPreview: 'Supervisor confirms the incident occurred during normal warehouse operations on the morning shift. Safety protocols were followed...'
      }
    ],
    createdAt: '2026-02-20T10:45:00Z'
  };

  const paramsSubject = new Subject<Record<string, string>>();

  const mockDocumentService = {
    getDocumentById: vi.fn(),
    deleteDocument: vi.fn(),
    queryDocuments: vi.fn()
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    mockDocumentService.getDocumentById.mockReturnValue(of(mockDocumentDetail));

    await TestBed.configureTestingModule({
      imports: [DocumentResultComponent],
      providers: [
        provideRouter([]),
        { provide: DocumentService, useValue: mockDocumentService },
        {
          provide: ActivatedRoute,
          useValue: {
            params: of({ id: '501' }),
            snapshot: { paramMap: { get: () => '501' } }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentResultComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
    expect(component.showDeleteModal()).toBe(false);
    expect(component.error()).toBeNull();
  });

  it('should load document on init', () => {
    expect(mockDocumentService.getDocumentById).toHaveBeenCalledWith(501);
    expect(component.document()).toEqual(mockDocumentDetail);
    expect(component.isLoading()).toBe(false);
    expect(component.documentId()).toBe(501);
  });

  it('should toggle chunk expansion', () => {
    expect(component.expandedChunks()).toEqual([]);

    component.toggleChunk(0);
    expect(component.expandedChunks()).toContain(0);

    component.toggleChunk(1);
    expect(component.expandedChunks()).toEqual([0, 1]);

    component.toggleChunk(0);
    expect(component.expandedChunks()).toEqual([1]);
  });

  it('should open delete confirmation modal', () => {
    expect(component.showDeleteModal()).toBe(false);

    component.showDeleteModal.set(true);
    expect(component.showDeleteModal()).toBe(true);

    component.showDeleteModal.set(false);
    expect(component.showDeleteModal()).toBe(false);
  });
});
