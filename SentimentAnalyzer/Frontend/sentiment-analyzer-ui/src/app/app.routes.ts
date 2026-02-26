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
import { authGuard } from './guards/auth.guard';
import { guestGuard } from './guards/guest.guard';

export const routes: Routes = [
  { path: '', component: LandingComponent },
  { path: 'sentiment', component: SentimentAnalyzer, canActivate: [authGuard] },
  { path: 'login', component: LoginComponent, canActivate: [guestGuard] },
  { path: 'insurance', component: InsuranceAnalyzerComponent, canActivate: [authGuard] },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'claims/triage', component: ClaimsTriageComponent, canActivate: [authGuard] },
  { path: 'claims/history', component: ClaimsHistoryComponent, canActivate: [authGuard] },
  { path: 'claims/:id', component: ClaimResultComponent, canActivate: [authGuard] },
  { path: 'dashboard/providers', component: ProviderHealthComponent, canActivate: [authGuard] },
  { path: 'dashboard/fraud', component: FraudAlertsComponent, canActivate: [authGuard] },
  { path: 'documents/upload', component: DocumentUploadComponent, canActivate: [authGuard] },
  { path: 'documents/query', component: DocumentQueryComponent, canActivate: [authGuard] },
  { path: 'documents/:id', component: DocumentResultComponent, canActivate: [authGuard] },
  { path: 'cx/copilot', component: CxCopilotComponent, canActivate: [authGuard] },
  { path: 'fraud/correlations/:claimId', component: FraudCorrelationComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '' },
];
