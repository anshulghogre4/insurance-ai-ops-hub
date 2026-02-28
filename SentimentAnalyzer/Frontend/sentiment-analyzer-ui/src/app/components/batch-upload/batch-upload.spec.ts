import { vi, describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { BatchUploadComponent } from './batch-upload';
import { ClaimsService } from '../../services/claims.service';
import { BatchClaimUploadResult } from '../../models/claims.model';
import { of, throwError } from 'rxjs';

describe('BatchUploadComponent', () => {
  let component: BatchUploadComponent;
  let fixture: ComponentFixture<BatchUploadComponent>;
  let claimsService: ClaimsService;

  const mockBatchResult: BatchClaimUploadResult = {
    batchId: 'BATCH-20260228-A1B2C3D4',
    totalCount: 5,
    processedCount: 5,
    successCount: 3,
    errorCount: 2,
    status: 'Completed',
    results: [
      { rowNumber: 2, claimId: 'CLM-2024-001', severity: 'High', fraudScore: 42, status: 'Triaged' },
      { rowNumber: 3, claimId: 'CLM-2024-002', severity: 'Medium', fraudScore: 25, status: 'Triaged' },
      { rowNumber: 5, claimId: 'CLM-2024-004', severity: 'Critical', fraudScore: 78, status: 'Triaged' }
    ],
    errors: [
      { rowNumber: 4, field: 'ClaimId', errorMessage: 'ClaimId is required and cannot be empty.' },
      { rowNumber: 6, field: 'EstimatedAmount', errorMessage: "EstimatedAmount 'not-a-number' is not a valid positive number." }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BatchUploadComponent, HttpClientTestingModule, RouterTestingModule]
    }).compileComponents();

    fixture = TestBed.createComponent(BatchUploadComponent);
    component = fixture.componentInstance;
    claimsService = TestBed.inject(ClaimsService);
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with empty state', () => {
    expect(component.selectedFile()).toBeNull();
    expect(component.isLoading()).toBe(false);
    expect(component.result()).toBeNull();
    expect(component.error()).toBeNull();
    expect(component.csvHeaders()).toEqual([]);
    expect(component.csvPreview()).toEqual([]);
  });

  it('should reject non-CSV files', () => {
    const pdfFile = new File(['content'], 'report.pdf', { type: 'application/pdf' });
    const event = { target: { files: [pdfFile], value: '' } } as unknown as Event;
    component.onFileSelected(event);

    expect(component.selectedFile()).toBeNull();
    expect(component.fileWarning()).toContain('CSV');
  });

  it('should reject files exceeding 5 MB', () => {
    const bigContent = new Uint8Array(6 * 1024 * 1024);
    const bigFile = new File([bigContent], 'big-claims.csv', { type: 'text/csv' });
    const event = { target: { files: [bigFile], value: '' } } as unknown as Event;
    component.onFileSelected(event);

    expect(component.selectedFile()).toBeNull();
    expect(component.fileWarning()).toContain('5 MB');
  });

  it('should accept valid CSV file and set selectedFile', () => {
    const csvContent = 'ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate\nCLM-001,Auto,Rear-end collision,5000,2024-01-15';
    const csvFile = new File([csvContent], 'claims-batch.csv', { type: 'text/csv' });
    const event = { target: { files: [csvFile], value: '' } } as unknown as Event;
    component.onFileSelected(event);

    expect(component.selectedFile()).toBeTruthy();
    expect(component.selectedFile()!.name).toBe('claims-batch.csv');
    expect(component.fileWarning()).toBeNull();
  });

  it('should remove selected file', () => {
    const csvFile = new File(['header\ndata'], 'test.csv', { type: 'text/csv' });
    const event = { target: { files: [csvFile], value: '' } } as unknown as Event;
    component.onFileSelected(event);

    expect(component.selectedFile()).toBeTruthy();

    component.removeFile();

    expect(component.selectedFile()).toBeNull();
    expect(component.csvHeaders()).toEqual([]);
    expect(component.csvPreview()).toEqual([]);
  });

  it('should submit batch and display results', () => {
    vi.spyOn(claimsService, 'uploadBatch').mockReturnValue(of(mockBatchResult));

    const csvFile = new File(['data'], 'claims.csv', { type: 'text/csv' });
    component.selectedFile.set(csvFile);

    component.submitBatch();

    expect(component.result()).toBeTruthy();
    expect(component.result()!.batchId).toBe('BATCH-20260228-A1B2C3D4');
    expect(component.result()!.successCount).toBe(3);
    expect(component.result()!.errorCount).toBe(2);
    expect(component.result()!.results.length).toBe(3);
    expect(component.result()!.errors.length).toBe(2);
    expect(component.isLoading()).toBe(false);
  });

  it('should handle submission error', () => {
    vi.spyOn(claimsService, 'uploadBatch').mockReturnValue(
      throwError(() => ({ status: 500, error: { error: 'Service temporarily unavailable.' } }))
    );

    const csvFile = new File(['data'], 'claims.csv', { type: 'text/csv' });
    component.selectedFile.set(csvFile);

    component.submitBatch();

    expect(component.error()).toBeTruthy();
    expect(component.error()).toContain('unavailable');
    expect(component.isLoading()).toBe(false);
  });

  it('should not submit when no file is selected', () => {
    const spy = vi.spyOn(claimsService, 'uploadBatch');
    component.submitBatch();
    expect(spy).not.toHaveBeenCalled();
  });

  it('should clear all state on clearAll', () => {
    component.selectedFile.set(new File(['data'], 'claims.csv', { type: 'text/csv' }));
    component.result.set(mockBatchResult);
    component.error.set('Some error');
    component.csvHeaders.set(['ClaimId', 'ClaimType']);
    component.csvPreview.set([['CLM-001', 'Auto']]);

    component.clearAll();

    expect(component.selectedFile()).toBeNull();
    expect(component.result()).toBeNull();
    expect(component.error()).toBeNull();
    expect(component.csvHeaders()).toEqual([]);
    expect(component.csvPreview()).toEqual([]);
  });

  it('should return correct severity class for result items', () => {
    expect(component.getSeverityClass('Critical')).toContain('rose');
    expect(component.getSeverityClass('High')).toContain('orange');
    expect(component.getSeverityClass('Medium')).toContain('amber');
    expect(component.getSeverityClass('Low')).toContain('emerald');
  });

  it('should return correct fraud score color', () => {
    expect(component.getFraudScoreColor(80)).toBe('text-rose-400');
    expect(component.getFraudScoreColor(60)).toBe('text-orange-400');
    expect(component.getFraudScoreColor(40)).toBe('text-amber-400');
    expect(component.getFraudScoreColor(10)).toBe('text-emerald-400');
  });

  it('should format file size correctly', () => {
    expect(component.formatFileSize(500)).toBe('500 B');
    expect(component.formatFileSize(2048)).toBe('2.0 KB');
    expect(component.formatFileSize(1048576)).toBe('1.0 MB');
    expect(component.formatFileSize(3145728)).toBe('3.0 MB');
  });

  it('should render results table when result has items', () => {
    component.result.set(mockBatchResult);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Batch Processing Complete');
    expect(compiled.textContent).toContain('BATCH-20260228-A1B2C3D4');
    expect(compiled.textContent).toContain('CLM-2024-001');
    expect(compiled.textContent).toContain('CLM-2024-002');
    expect(compiled.textContent).toContain('CLM-2024-004');
  });

  it('should render error rows in amber when result has errors', () => {
    component.result.set(mockBatchResult);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Validation Errors (2)');
    expect(compiled.textContent).toContain('ClaimId is required');
    expect(compiled.textContent).toContain('not-a-number');
  });

  it('should show summary card with correct counts', () => {
    component.result.set(mockBatchResult);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('5'); // total
    expect(compiled.textContent).toContain('3'); // success
    expect(compiled.textContent).toContain('2'); // errors
    expect(compiled.textContent).toContain('Completed');
  });
});
