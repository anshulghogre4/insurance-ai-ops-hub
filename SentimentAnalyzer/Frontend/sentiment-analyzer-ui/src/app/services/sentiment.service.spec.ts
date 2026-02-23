import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { SentimentService } from './sentiment.service';
import { SentimentResponse } from '../models/sentiment.model';

describe('SentimentService', () => {
  let service: SentimentService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [SentimentService]
    });
    service = TestBed.inject(SentimentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should send POST request to analyze sentiment', () => {
    const testText = 'I love this product!';
    const mockResponse: SentimentResponse = {
      sentiment: 'Positive',
      confidenceScore: 0.95,
      explanation: 'The text expresses strong positive emotions',
      emotionBreakdown: { joy: 0.9, satisfaction: 0.85 }
    };

    service.analyzeSentiment(testText).subscribe(response => {
      expect(response).toEqual(mockResponse);
    });

    const req = httpMock.expectOne('http://localhost:5143/api/sentiment/analyze');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ text: testText });
    req.flush(mockResponse);
  });

  it('should handle error response', () => {
    const testText = 'Test text';
    const errorMessage = 'Server error';

    service.analyzeSentiment(testText).subscribe({
      next: () => { throw new Error('should have failed with 500 error'); },
      error: (error) => {
        expect(error.status).toBe(500);
      }
    });

    const req = httpMock.expectOne('http://localhost:5143/api/sentiment/analyze');
    req.flush(errorMessage, { status: 500, statusText: 'Server Error' });
  });
});
