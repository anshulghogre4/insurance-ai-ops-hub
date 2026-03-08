export interface ClaimTriagedEvent {
  claimId: number;
  severity: string;
  personaType: string;
  claimType: string;
  fraudScore: number;
  triagedAt: string;
}

export interface ClaimStatusEvent {
  claimId: number;
  oldStatus: string;
  newStatus: string;
  changedAt: string;
}

export interface FraudAlertEvent {
  claimId: number;
  fraudScore: number;
  flags: string[];
  riskLevel: string;
  detectedAt: string;
}

export interface ProviderStatusEvent {
  providerName: string;
  oldStatus: string;
  newStatus: string;
  cooldownSeconds: number | null;
  changedAt: string;
}

export interface HealthSnapshotEvent {
  providers: ProviderStatusEvent[];
  checkedAt: string;
}

export interface AnalyticsMetrics {
  claimsPerHour: number;
  avgTriageMs: number;
  fraudDetectionRate: number;
  providerResponseMs: Record<string, number>;
  docQueriesPerHour: number;
  windowStart: string;
  windowEnd: string;
}
