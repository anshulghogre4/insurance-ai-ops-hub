import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { DocumentQueryComponent } from './document-query';
import { DocumentService } from '../../services/document.service';
import { DocumentQueryResult } from '../../models/document.model';

describe('DocumentQueryComponent', () => {
  let fixture: ComponentFixture<DocumentQueryComponent>;
  let component: DocumentQueryComponent;

  const mockQueryResult: DocumentQueryResult = {
    answer: 'The flood damage exclusion applies to all standard homeowners policies unless a separate flood endorsement is purchased. Coverage A limits do not extend to flood-related losses.',
    confidence: 0.87,
    citations: [
      {
        documentId: 201,
        fileName: 'homeowners-policy-HO3-2026.pdf',
        sectionName: 'Section I - Exclusions',
        chunkIndex: 9,
        relevantText: 'This policy does not cover loss caused by flood, surface water, waves, tidal water, or overflow of any body of water.',
        similarity: 0.92
      }
    ],
    llmProvider: 'Groq',
    elapsedMilliseconds: 1830,
    answerSafety: null
  };

  const mockDocumentService = {
    queryDocuments: vi.fn(),
    getDocumentHistory: vi.fn().mockReturnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0 }))
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    mockDocumentService.getDocumentHistory.mockReturnValue(
      of({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0 })
    );

    await TestBed.configureTestingModule({
      imports: [DocumentQueryComponent],
      providers: [
        provideRouter([]),
        { provide: DocumentService, useValue: mockDocumentService },
        {
          provide: ActivatedRoute,
          useValue: {
            queryParams: of({}),
            snapshot: { paramMap: { get: () => null } }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentQueryComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
    expect(component.isQuerying()).toBe(false);
    expect(component.queryResult()).toBeNull();
    expect(component.error()).toBeNull();
    expect(component.question).toBe('');
  });

  it('should not submit when question is empty', () => {
    component.question = '   ';
    component.submitQuery();

    expect(mockDocumentService.queryDocuments).not.toHaveBeenCalled();
    expect(component.isQuerying()).toBe(false);
  });

  it('should call queryDocuments on submit with a valid question', () => {
    mockDocumentService.queryDocuments.mockReturnValue(of(mockQueryResult));

    component.question = 'What are the flood damage exclusions in the homeowners policy?';
    component.selectedDocumentId = 201;
    component.submitQuery();

    expect(mockDocumentService.queryDocuments).toHaveBeenCalledWith(
      'What are the flood damage exclusions in the homeowners policy?',
      201
    );
    expect(component.queryResult()).toEqual(mockQueryResult);
    expect(component.isQuerying()).toBe(false);
  });

  it('should toggle citation expansion', () => {
    expect(component.expandedCitations()).toEqual([]);

    component.toggleCitation(0);
    expect(component.expandedCitations()).toContain(0);

    component.toggleCitation(2);
    expect(component.expandedCitations()).toContain(0);
    expect(component.expandedCitations()).toContain(2);

    component.toggleCitation(0);
    expect(component.expandedCitations()).not.toContain(0);
    expect(component.expandedCitations()).toContain(2);
  });

  it('should NOT show content safety warning when answerSafety is null', () => {
    mockDocumentService.queryDocuments.mockReturnValue(of(mockQueryResult));

    component.question = 'What are the policy exclusions?';
    component.submitQuery();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const warningBanner = el.querySelector('[aria-label="Content safety warning"]');
    expect(warningBanner).toBeNull();
  });

  it('should show content safety warning banner when answer is flagged', () => {
    const flaggedResult: DocumentQueryResult = {
      ...mockQueryResult,
      answerSafety: {
        isSafe: false,
        flaggedCategories: ['Hate', 'Violence'],
        provider: 'Azure Content Safety'
      }
    };
    mockDocumentService.queryDocuments.mockReturnValue(of(flaggedResult));

    component.question = 'What are the policy exclusions?';
    component.submitQuery();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const warningBanner = el.querySelector('[aria-label="Content safety warning"]');
    expect(warningBanner).toBeTruthy();
    expect(warningBanner?.textContent).toContain('Content Safety Warning');
    expect(warningBanner?.textContent).toContain('Hate, Violence');
  });

  it('should show safety warning with correct flagged categories', () => {
    const flaggedResult: DocumentQueryResult = {
      ...mockQueryResult,
      answerSafety: {
        isSafe: false,
        flaggedCategories: ['SelfHarm', 'Sexual'],
        provider: 'Azure Content Safety'
      }
    };
    mockDocumentService.queryDocuments.mockReturnValue(of(flaggedResult));

    component.question = 'What are the property claim procedures?';
    component.submitQuery();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const warningBanner = el.querySelector('[aria-label="Content safety warning"]');
    expect(warningBanner).toBeTruthy();
    expect(warningBanner?.textContent).toContain('SelfHarm, Sexual');
  });
});
