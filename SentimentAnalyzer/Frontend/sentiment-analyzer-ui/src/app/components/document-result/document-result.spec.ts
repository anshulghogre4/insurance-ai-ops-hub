import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { of, throwError, Subject } from 'rxjs';
import { DocumentResultComponent } from './document-result';
import { DocumentService } from '../../services/document.service';
import { DocumentDetailResult, SyntheticQAResult } from '../../models/document.model';

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
        contentPreview: 'Employee John Rivera sustained a lower back injury on 2026-01-15 while lifting a 50lb equipment crate at the warehouse...',
        pageNumber: 1,
        parentChunkId: null,
        chunkLevel: 0,
        isSafe: true,
        safetyFlags: null
      },
      {
        chunkIndex: 1,
        sectionName: 'Medical Evaluation',
        tokenCount: 410,
        contentPreview: 'Diagnosis: L4-L5 disc herniation confirmed via MRI. Treatment plan includes physical therapy 3x/week and epidural injection...',
        pageNumber: 2,
        parentChunkId: null,
        chunkLevel: 0,
        isSafe: true,
        safetyFlags: null
      },
      {
        chunkIndex: 2,
        sectionName: 'Employer Statement',
        tokenCount: 198,
        contentPreview: 'Supervisor confirms the incident occurred during normal warehouse operations on the morning shift. Safety protocols were followed...',
        pageNumber: 3,
        parentChunkId: null,
        chunkLevel: 0,
        isSafe: false,
        safetyFlags: 'Violence|SelfHarm'
      }
    ],
    createdAt: '2026-02-20T10:45:00Z'
  };

  const paramsSubject = new Subject<Record<string, string>>();

  const mockQAResult: SyntheticQAResult = {
    documentId: 501,
    documentName: 'workers-comp-claim-WC-2026-33012.pdf',
    totalPairsGenerated: 3,
    pairs: [
      {
        id: 1,
        chunkId: 1,
        question: 'What type of injury did the employee sustain?',
        answer: 'The employee sustained a lower back injury (L4-L5 disc herniation) while lifting a 50lb equipment crate.',
        category: 'factual',
        confidence: 0.95,
        sectionName: 'Incident Report'
      },
      {
        id: 2,
        chunkId: 2,
        question: 'How would the treatment plan impact the workers compensation claim timeline?',
        answer: 'Physical therapy 3x/week and epidural injection suggest a prolonged recovery period, extending the claim duration by 6-12 months.',
        category: 'inferential',
        confidence: 0.88,
        sectionName: 'Medical Evaluation'
      },
      {
        id: 3,
        chunkId: 3,
        question: 'What steps should the employer take following this workplace injury?',
        answer: 'The employer must: 1) File OSHA incident report within 24 hours, 2) Conduct safety review, 3) Update lifting protocols, 4) Provide modified duty options.',
        category: 'procedural',
        confidence: 0.91,
        sectionName: 'Employer Statement'
      }
    ],
    llmProvider: 'Groq',
    elapsedMilliseconds: 3200,
    errorMessage: null
  };

  const mockDocumentService = {
    getDocumentById: vi.fn(),
    deleteDocument: vi.fn(),
    queryDocuments: vi.fn(),
    generateQAPairs: vi.fn(),
    getQAPairs: vi.fn()
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    mockDocumentService.getDocumentById.mockReturnValue(of(mockDocumentDetail));
    mockDocumentService.getQAPairs.mockReturnValue(throwError(() => ({ status: 404 })));
    mockDocumentService.generateQAPairs.mockReturnValue(of(mockQAResult));

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

  it('should render "Safe" badge for safe chunks', () => {
    const el = fixture.nativeElement as HTMLElement;
    const safeBadges = el.querySelectorAll('[aria-label="Content safe"]');
    // 2 safe chunks in mock data
    expect(safeBadges.length).toBe(2);
    safeBadges.forEach(badge => {
      expect(badge.textContent?.trim()).toBe('Safe');
      expect(badge.classList.contains('badge-success')).toBe(true);
    });
  });

  it('should render "Flagged" badge for flagged chunks', () => {
    const el = fixture.nativeElement as HTMLElement;
    const flaggedBadges = el.querySelectorAll('[aria-label="Content flagged"]');
    // 1 flagged chunk in mock data
    expect(flaggedBadges.length).toBe(1);
    expect(flaggedBadges[0].textContent?.trim()).toBe('Flagged: Violence, SelfHarm');
    expect(flaggedBadges[0].classList.contains('badge-danger')).toBe(true);
  });

  it('should show content safety warning banner when inline query answer is flagged', () => {
    component.queryResult.set({
      answer: 'This is a flagged response about policy exclusions.',
      confidence: 0.75,
      citations: [],
      llmProvider: 'Groq',
      elapsedMilliseconds: 1200,
      answerSafety: {
        isSafe: false,
        flaggedCategories: ['Hate', 'Violence'],
        provider: 'Azure Content Safety'
      }
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const warningBanner = el.querySelector('[aria-label="Content safety warning"]');
    expect(warningBanner).toBeTruthy();
    expect(warningBanner?.textContent).toContain('Content Safety Warning');
    expect(warningBanner?.textContent).toContain('Hate, Violence');
  });

  it('should NOT show content safety warning when answerSafety is null', () => {
    component.queryResult.set({
      answer: 'Safe answer about policy coverage.',
      confidence: 0.87,
      citations: [],
      llmProvider: 'Groq',
      elapsedMilliseconds: 1500,
      answerSafety: null
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const warningBanner = el.querySelector('[aria-label="Content safety warning"]');
    expect(warningBanner).toBeNull();
  });

  // ===================== Fine-Tuning Q&A Tests =====================

  it('should show "No training data yet" when no Q&A pairs exist', () => {
    const el = fixture.nativeElement as HTMLElement;
    const emptyState = el.querySelector('[aria-label="No training data"]');
    expect(emptyState).toBeTruthy();
    expect(emptyState?.textContent).toContain('No training data yet');
  });

  it('should render Q&A pairs when loaded', () => {
    component.qaPairs.set(mockQAResult.pairs);
    component.qaProvider.set('Groq');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    // Check pairs count badge
    expect(el.textContent).toContain('3 Training Pairs Generated');
    // Check questions are visible
    expect(el.textContent).toContain('What type of injury did the employee sustain?');
    expect(el.textContent).toContain('How would the treatment plan impact the workers compensation claim timeline?');
    expect(el.textContent).toContain('What steps should the employer take following this workplace injury?');
    // Check provider badge
    expect(el.querySelector('[aria-label="Fine-tuning training data"]')?.textContent).toContain('Groq');
  });

  it('should show loading state on Generate button', () => {
    component.qaLoading.set(true);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const generateBtn = el.querySelector('[aria-label="Generate QA training pairs"]');
    expect(generateBtn).toBeTruthy();
    expect(generateBtn?.textContent).toContain('Generating...');
    expect((generateBtn as HTMLButtonElement)?.disabled).toBe(true);
  });

  it('should render correct category badge colors', () => {
    component.qaPairs.set(mockQAResult.pairs);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const badges = el.querySelectorAll('[aria-label="Fine-tuning training data"] .badge');

    // Find category badges by their text
    const factualBadge = Array.from(badges).find(b => b.textContent?.trim() === 'factual');
    const inferentialBadge = Array.from(badges).find(b => b.textContent?.trim() === 'inferential');
    const proceduralBadge = Array.from(badges).find(b => b.textContent?.trim() === 'procedural');

    expect(factualBadge?.classList.contains('text-indigo-400')).toBe(true);
    expect(inferentialBadge?.classList.contains('text-amber-400')).toBe(true);
    expect(proceduralBadge?.classList.contains('text-emerald-400')).toBe(true);
  });

  it('should show error message on Q&A generation failure', () => {
    component.qaError.set('Failed to generate Q&A pairs. All LLM providers are down.');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const errorAlert = el.querySelector('[aria-label="QA generation error"]');
    expect(errorAlert).toBeTruthy();
    expect(errorAlert?.textContent).toContain('Failed to generate Q&A pairs');
  });

  it('should call generateQAPairs on button click', () => {
    const el = fixture.nativeElement as HTMLElement;
    const generateBtn = el.querySelector('[aria-label="Generate QA training pairs"]') as HTMLButtonElement;
    expect(generateBtn).toBeTruthy();

    generateBtn.click();
    fixture.detectChanges();

    expect(mockDocumentService.generateQAPairs).toHaveBeenCalledWith(501);
    expect(component.qaPairs().length).toBe(3);
    expect(component.qaProvider()).toBe('Groq');
  });

  it('should toggle Q&A pair expansion', () => {
    expect(component.expandedQAPairs()).toEqual([]);

    component.toggleQAPair(1);
    expect(component.expandedQAPairs()).toContain(1);

    component.toggleQAPair(2);
    expect(component.expandedQAPairs()).toEqual([1, 2]);

    component.toggleQAPair(1);
    expect(component.expandedQAPairs()).toEqual([2]);
  });

  it('should return correct CSS classes for QA category badges', () => {
    expect(component.getQACategoryBadge('factual')).toContain('text-indigo-400');
    expect(component.getQACategoryBadge('inferential')).toContain('text-amber-400');
    expect(component.getQACategoryBadge('procedural')).toContain('text-emerald-400');
    expect(component.getQACategoryBadge('unknown')).toBe('badge-neutral');
  });
});
