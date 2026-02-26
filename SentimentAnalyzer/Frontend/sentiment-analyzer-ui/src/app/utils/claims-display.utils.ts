/**
 * Shared display utility functions for claims-related components.
 * Pure functions extracted from claims-triage, claim-result, claims-history, and fraud-alerts
 * to eliminate duplicated styling/formatting logic.
 */

/** Returns Tailwind classes for a severity badge (colored pill with shadow). */
export function getSeverityClass(severity: string): string {
  switch (severity) {
    case 'Critical': return 'bg-rose-500 shadow-lg shadow-rose-500/30';
    case 'High': return 'bg-orange-500 shadow-lg shadow-orange-500/25';
    case 'Medium': return 'bg-amber-500 shadow-lg shadow-amber-500/20';
    case 'Low': return 'bg-emerald-500 shadow-lg shadow-emerald-500/20';
    default: return 'bg-slate-500';
  }
}

/** Returns a badge class string for an urgency level. */
export function getUrgencyBadge(urgency: string): string {
  switch (urgency) {
    case 'Immediate': return 'badge-danger';
    case 'Urgent': return 'badge-warning';
    case 'Standard': return 'badge-info';
    case 'Low': return 'badge-success';
    default: return 'badge-neutral';
  }
}

/** Returns a text color class based on the fraud score value. */
export function getFraudScoreColor(score: number): string {
  if (score >= 75) return 'text-rose-400';
  if (score >= 55) return 'text-orange-400';
  if (score >= 30) return 'text-amber-400';
  return 'text-emerald-400';
}

/** Returns a badge class for a fraud risk level label. */
export function getFraudRiskBadge(riskLevel: string): string {
  switch (riskLevel) {
    case 'VeryHigh': return 'badge-danger';
    case 'High': return 'badge-danger';
    case 'Medium': return 'badge-warning';
    case 'Low': return 'badge-success';
    case 'VeryLow': return 'badge-success';
    default: return 'badge-neutral';
  }
}

/** Returns a CSS linear-gradient string for the fraud gauge bar. */
export function getFraudGaugeGradient(score: number): string {
  if (score >= 75) return 'linear-gradient(90deg, #f59e0b, #ef4444)';
  if (score >= 55) return 'linear-gradient(90deg, #eab308, #f97316)';
  if (score >= 30) return 'linear-gradient(90deg, #22c55e, #eab308)';
  return 'linear-gradient(90deg, #10b981, #22c55e)';
}

/** Returns a badge class for an action priority level. */
export function getPriorityBadge(priority: string): string {
  switch (priority?.toLowerCase()) {
    case 'critical': return 'badge-danger';
    case 'high': return 'badge-warning';
    case 'medium': return 'badge-info';
    default: return 'badge-neutral';
  }
}

/** Returns a badge class for a fraud indicator category. */
export function getCategoryBadge(category: string): string {
  const map: Record<string, string> = {
    Timing: 'badge-info',
    Behavioral: 'badge-warning',
    Financial: 'badge-danger',
    Pattern: 'badge-warning',
    Documentation: 'badge-neutral'
  };
  return map[category] || 'badge-neutral';
}

/**
 * Returns an effective fraud score that reconciles mismatches between the
 * numeric fraudScore and the textual fraudRiskLevel returned by the LLM.
 * When the score is 0 (or unreasonably low for the stated risk level),
 * a minimum floor is derived from the risk level string so the gauge bar
 * renders consistently with the badge.
 */
export function getEffectiveFraudScore(score: number, riskLevel: string): number {
  const floors: Record<string, number> = {
    VeryHigh: 85,
    High: 70,
    Medium: 45,
    Low: 20,
    VeryLow: 5
  };
  const floor = floors[riskLevel] ?? 0;
  return Math.max(score, floor);
}

/**
 * Formats an ISO date string into a human-readable locale string.
 * @param dateStr - ISO 8601 date string
 * @param includeYear - Whether to include the year in the output (default: true)
 */
export function formatClaimDate(dateStr: string, includeYear: boolean = true): string {
  const options: Intl.DateTimeFormatOptions = includeYear
    ? { month: 'short', day: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit' }
    : { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' };
  return new Date(dateStr).toLocaleDateString('en-US', options);
}
