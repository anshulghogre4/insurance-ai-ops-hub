export interface SentimentRequest {
  text: string;
}

export interface SentimentResponse {
  sentiment: string;
  confidenceScore: number;
  explanation: string;
  emotionBreakdown: { [key: string]: number };
}
