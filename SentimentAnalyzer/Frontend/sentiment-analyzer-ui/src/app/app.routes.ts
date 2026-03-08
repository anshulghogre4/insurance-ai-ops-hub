import { Routes } from '@angular/router';
import { LandingComponent } from './components/landing/landing';
import { SentimentAnalyzer } from './components/sentiment-analyzer/sentiment-analyzer';
import { InsuranceAnalyzerComponent } from './components/insurance-analyzer/insurance-analyzer';
import { DashboardComponent } from './components/dashboard/dashboard';
import { LoginComponent } from './components/login/login';
import { ClaimsTriageComponent } from './components/claims-triage/claims-triage';
import { ClaimsHistoryComponent } from './components/claims-history/claims-history';
import { ClaimResultComponent } from './components/claim-result/claim-result';
import { ProviderHealthComponent } from './components/provider-health/provider-health';
import { FraudAlertsComponent } from './components/fraud-alerts/fraud-alerts';
import { DocumentUploadComponent } from './components/document-upload/document-upload';
import { DocumentQueryComponent } from './components/document-query/document-query';
import { DocumentResultComponent } from './components/document-result/document-result';
import { CxCopilotComponent } from './components/cx-copilot/cx-copilot';
import { FraudCorrelationComponent } from './components/fraud-correlation/fraud-correlation';
import { BatchUploadComponent } from './components/batch-upload/batch-upload';
import { DocumentLibraryComponent } from './components/document-library/document-library';
import { LiveDashboardComponent } from './components/live-dashboard/live-dashboard';
import { authGuard } from './guards/auth.guard';
import { guestGuard } from './guards/guest.guard';

export const routes: Routes = [
  { path: '', component: LandingComponent },
  { path: 'sentiment', component: SentimentAnalyzer, canActivate: [authGuard], data: { breadcrumb: 'Sentiment Analysis' } },
  { path: 'login', component: LoginComponent, canActivate: [guestGuard] },
  { path: 'insurance', component: InsuranceAnalyzerComponent, canActivate: [authGuard], data: { breadcrumb: 'Insurance Analysis' } },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard], data: { breadcrumb: 'Dashboard' } },
  { path: 'claims/triage', component: ClaimsTriageComponent, canActivate: [authGuard], data: { breadcrumb: 'New Triage' } },
  { path: 'claims/history', component: ClaimsHistoryComponent, canActivate: [authGuard], data: { breadcrumb: 'History' } },
  { path: 'claims/batch', component: BatchUploadComponent, canActivate: [authGuard], data: { breadcrumb: 'Batch Upload' } },
  { path: 'claims/:id', component: ClaimResultComponent, canActivate: [authGuard], data: { breadcrumb: 'Claim :id' } },
  { path: 'dashboard/providers', component: ProviderHealthComponent, canActivate: [authGuard], data: { breadcrumb: 'Provider Health' } },
  { path: 'dashboard/fraud', component: FraudAlertsComponent, canActivate: [authGuard], data: { breadcrumb: 'Fraud Alerts' } },
  { path: 'documents', component: DocumentLibraryComponent, canActivate: [authGuard], data: { breadcrumb: 'Library' } },
  { path: 'documents/upload', component: DocumentUploadComponent, canActivate: [authGuard], data: { breadcrumb: 'Upload' } },
  { path: 'documents/query', component: DocumentQueryComponent, canActivate: [authGuard], data: { breadcrumb: 'Query' } },
  { path: 'documents/:id', component: DocumentResultComponent, canActivate: [authGuard], data: { breadcrumb: 'Document :id' } },
  { path: 'cx/copilot', component: CxCopilotComponent, canActivate: [authGuard], data: { breadcrumb: 'CX Copilot' } },
  { path: 'fraud/correlations/:claimId', component: FraudCorrelationComponent, canActivate: [authGuard], data: { breadcrumb: 'Correlations' } },
  { path: 'dashboard/live', component: LiveDashboardComponent, canActivate: [authGuard], data: { breadcrumb: 'Live' } },
  { path: '**', redirectTo: '' },
];
