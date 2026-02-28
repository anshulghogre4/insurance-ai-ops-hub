import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DocumentUploadComponent } from './document-upload';
import { DocumentService } from '../../services/document.service';
import { DocumentUploadResult, DocumentProgressEvent } from '../../models/document.model';

describe('DocumentUploadComponent', () => {
  let fixture: ComponentFixture<DocumentUploadComponent>;
  let component: DocumentUploadComponent;

  const mockUploadResult: DocumentUploadResult = {
    documentId: 301,
    fileName: 'homeowners-policy-renewal-2026.pdf',
    status: 'Processed',
    pageCount: 8,
    chunkCount: 24,
    embeddingProvider: 'Groq',
    errorMessage: null
  };

  /** SSE progress events ending with a Done event carrying the result. */
  const mockProgressEvents: DocumentProgressEvent[] = [
    { phase: 'Uploading', progress: 10, message: 'Uploading document...', result: null, errorMessage: null },
    { phase: 'OCR', progress: 30, message: 'Extracting text via OCR...', result: null, errorMessage: null },
    { phase: 'Chunking', progress: 50, message: 'Splitting into sections...', result: null, errorMessage: null },
    { phase: 'Embedding', progress: 75, message: 'Generating embeddings...', result: null, errorMessage: null },
    { phase: 'Safety', progress: 90, message: 'Safety check...', result: null, errorMessage: null },
    { phase: 'Done', progress: 100, message: 'Complete', result: mockUploadResult, errorMessage: null },
  ];

  const mockDocumentService = {
    uploadDocument: vi.fn(),
    uploadDocumentWithProgress: vi.fn()
  };

  beforeEach(async () => {
    vi.clearAllMocks();

    await TestBed.configureTestingModule({
      imports: [DocumentUploadComponent],
      providers: [
        provideRouter([]),
        { provide: DocumentService, useValue: mockDocumentService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentUploadComponent);
    component = fixture.componentInstance;
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
    expect(component.isUploading()).toBe(false);
    expect(component.selectedFile()).toBeNull();
    expect(component.error()).toBeNull();
    expect(component.uploadResult()).toBeNull();
  });

  it('should accept file selection via onFileSelected', () => {
    const policyFile = new File(
      ['coverage declarations page content'],
      'commercial-general-liability-policy.pdf',
      { type: 'application/pdf' }
    );
    const mockEvent = {
      target: { files: [policyFile], value: '' }
    } as unknown as Event;

    component.onFileSelected(mockEvent);

    expect(component.selectedFile()).toBe(policyFile);
    expect(component.error()).toBeNull();
  });

  it('should call uploadDocumentWithProgress on the service when upload is triggered', () => {
    const claimDocument = new File(
      ['auto collision claim form and damage assessment report'],
      'auto-claim-CLM-2026-55891.pdf',
      { type: 'application/pdf' }
    );
    mockDocumentService.uploadDocumentWithProgress.mockReturnValue(of(...mockProgressEvents));

    component.selectedFile.set(claimDocument);
    component.category = 'Claim';
    component.uploadDocument();

    expect(mockDocumentService.uploadDocumentWithProgress).toHaveBeenCalledWith(claimDocument, 'Claim');
    expect(component.uploadResult()).toEqual(mockUploadResult);
    expect(component.isUploading()).toBe(false);
  });

  it('should display error on upload failure via SSE Error phase', () => {
    const endorsementFile = new File(
      ['endorsement amendment schedule'],
      'endorsement-EN-2026-7743.pdf',
      { type: 'application/pdf' }
    );
    const errorEvent: DocumentProgressEvent = {
      phase: 'Error',
      progress: 0,
      message: 'Processing failed.',
      result: null,
      errorMessage: 'Document processing failed. The uploaded PDF appears to be encrypted or password-protected.'
    };
    mockDocumentService.uploadDocumentWithProgress.mockReturnValue(of(errorEvent));

    component.selectedFile.set(endorsementFile);
    component.uploadDocument();

    expect(component.error()).toBe('Document processing failed. The uploaded PDF appears to be encrypted or password-protected.');
    expect(component.isUploading()).toBe(false);
    expect(component.uploadResult()).toBeNull();
  });

  it('should display error on observable error', () => {
    const endorsementFile = new File(
      ['endorsement amendment schedule'],
      'endorsement-EN-2026-7743.pdf',
      { type: 'application/pdf' }
    );
    mockDocumentService.uploadDocumentWithProgress.mockReturnValue(
      throwError(() => new Error('Network connection lost'))
    );

    component.selectedFile.set(endorsementFile);
    component.uploadDocument();

    expect(component.error()).toBe('Network connection lost');
    expect(component.isUploading()).toBe(false);
    expect(component.uploadResult()).toBeNull();
  });
});
