import { vi, describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { DocumentLibraryComponent } from './document-library';
import { DocumentService } from '../../services/document.service';
import { DocumentSummary } from '../../models/document.model';
import { PaginatedResponse } from '../../models/claims.model';
import { of, throwError } from 'rxjs';

describe('DocumentLibraryComponent', () => {
  let component: DocumentLibraryComponent;
  let fixture: ComponentFixture<DocumentLibraryComponent>;
  let documentService: DocumentService;

  const mockDocuments: PaginatedResponse<DocumentSummary> = {
    items: [
      {
        id: 1, fileName: 'homeowner-policy-2024.pdf', mimeType: 'application/pdf',
        category: 'Policy', status: 'Processed', pageCount: 15, chunkCount: 42,
        createdAt: '2026-02-20T14:30:00Z'
      },
      {
        id: 2, fileName: 'auto-claim-CLM-2024-00847.pdf', mimeType: 'application/pdf',
        category: 'Claim', status: 'Processed', pageCount: 3, chunkCount: 8,
        createdAt: '2026-02-22T09:15:00Z'
      },
      {
        id: 3, fileName: 'adjuster-correspondence.png', mimeType: 'image/png',
        category: 'Correspondence', status: 'Processed', pageCount: 1, chunkCount: 2,
        createdAt: '2026-02-25T16:45:00Z'
      }
    ],
    totalCount: 3,
    page: 1,
    pageSize: 12,
    totalPages: 1
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentLibraryComponent, HttpClientTestingModule, RouterTestingModule]
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentLibraryComponent);
    component = fixture.componentInstance;
    documentService = TestBed.inject(DocumentService);
  });

  it('should create', () => {
    vi.spyOn(documentService, 'getDocumentHistory').mockReturnValue(of(mockDocuments));
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should load documents on init', () => {
    vi.spyOn(documentService, 'getDocumentHistory').mockReturnValue(of(mockDocuments));
    fixture.detectChanges();

    expect(component.documents().length).toBe(3);
    expect(component.totalCount()).toBe(3);
    expect(component.isLoading()).toBe(false);
  });

  it('should set loading state initially', () => {
    vi.spyOn(documentService, 'getDocumentHistory').mockReturnValue(of(mockDocuments));
    // Before detectChanges, isLoading defaults to true
    expect(component.isLoading()).toBe(true);
  });

  it('should handle error from service', () => {
    vi.spyOn(documentService, 'getDocumentHistory').mockReturnValue(throwError(() => new Error('Network error')));
    fixture.detectChanges();

    expect(component.error()).toBe('Failed to load document library. Please try again.');
    expect(component.documents().length).toBe(0);
    expect(component.isLoading()).toBe(false);
  });

  it('should reset page to 1 on category change', () => {
    const spy = vi.spyOn(documentService, 'getDocumentHistory').mockReturnValue(of(mockDocuments));
    fixture.detectChanges();

    component.currentPage.set(3);
    const event = { target: { value: 'Policy' } } as unknown as Event;
    component.onCategoryChange(event);

    expect(component.selectedCategory()).toBe('Policy');
    expect(component.currentPage()).toBe(1);
    expect(spy).toHaveBeenCalledWith(expect.objectContaining({ category: 'Policy' }));
  });

  it('should not send category filter when All is selected', () => {
    const spy = vi.spyOn(documentService, 'getDocumentHistory').mockReturnValue(of(mockDocuments));
    fixture.detectChanges();

    const event = { target: { value: 'All' } } as unknown as Event;
    component.onCategoryChange(event);

    // The last call should not have a category property
    const lastCall = spy.mock.calls[spy.mock.calls.length - 1][0];
    expect(lastCall?.category).toBeUndefined();
  });

  it('should format dates correctly', () => {
    const result = component.formatDate('2026-02-20T14:30:00Z');
    expect(result).toContain('Feb');
    expect(result).toContain('20');
  });

  it('should identify image MIME types', () => {
    expect(component.isImageMime('image/png')).toBe(true);
    expect(component.isImageMime('image/jpeg')).toBe(true);
    expect(component.isImageMime('application/pdf')).toBe(false);
  });

  it('should return correct category badge classes', () => {
    expect(component.getCategoryBadge('Policy')).toBe('bg-indigo-500');
    expect(component.getCategoryBadge('Claim')).toBe('bg-amber-500');
    expect(component.getCategoryBadge('Endorsement')).toBe('bg-emerald-500');
    expect(component.getCategoryBadge('Correspondence')).toBe('bg-purple-500');
    expect(component.getCategoryBadge('Other')).toBe('bg-slate-500');
    expect(component.getCategoryBadge('Unknown')).toBe('bg-slate-500');
  });

  it('should return correct status badge classes', () => {
    expect(component.getStatusBadge('Processed')).toBe('bg-emerald-500');
    expect(component.getStatusBadge('Processing')).toBe('bg-amber-500');
    expect(component.getStatusBadge('Error')).toBe('bg-rose-500');
    expect(component.getStatusBadge('Unknown')).toBe('bg-slate-500');
  });

  it('should navigate pages correctly', () => {
    vi.spyOn(documentService, 'getDocumentHistory').mockReturnValue(of({
      ...mockDocuments,
      totalPages: 3,
      totalCount: 30
    }));
    fixture.detectChanges();

    component.nextPage();
    expect(component.currentPage()).toBe(2);

    component.nextPage();
    expect(component.currentPage()).toBe(3);

    // Should not go past total pages
    component.nextPage();
    expect(component.currentPage()).toBe(3);

    component.prevPage();
    expect(component.currentPage()).toBe(2);

    component.prevPage();
    expect(component.currentPage()).toBe(1);

    // Should not go below 1
    component.prevPage();
    expect(component.currentPage()).toBe(1);
  });

  it('should render document cards in template', () => {
    vi.spyOn(documentService, 'getDocumentHistory').mockReturnValue(of(mockDocuments));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('homeowner-policy-2024.pdf');
    expect(compiled.textContent).toContain('auto-claim-CLM-2024-00847.pdf');
    expect(compiled.textContent).toContain('adjuster-correspondence.png');
  });

  it('should show empty state when no documents', () => {
    vi.spyOn(documentService, 'getDocumentHistory').mockReturnValue(of({
      items: [], totalCount: 0, page: 1, pageSize: 12, totalPages: 0
    }));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No documents uploaded yet');
    expect(compiled.querySelector('a[href="/documents/upload"]')).toBeTruthy();
  });

  it('should show error banner on failure', () => {
    vi.spyOn(documentService, 'getDocumentHistory').mockReturnValue(throwError(() => new Error('fail')));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Failed to load document library');
    expect(compiled.querySelector('[aria-label="Retry loading documents"]')).toBeTruthy();
  });
});
