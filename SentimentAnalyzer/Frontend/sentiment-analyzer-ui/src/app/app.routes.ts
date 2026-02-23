import { Routes } from '@angular/router';
import { SentimentAnalyzer } from './components/sentiment-analyzer/sentiment-analyzer';
import { InsuranceAnalyzerComponent } from './components/insurance-analyzer/insurance-analyzer';
import { DashboardComponent } from './components/dashboard/dashboard';
import { LoginComponent } from './components/login/login';
import { authGuard } from './guards/auth.guard';
import { guestGuard } from './guards/guest.guard';

export const routes: Routes = [
  { path: '', component: SentimentAnalyzer, canActivate: [authGuard] },
  { path: 'login', component: LoginComponent, canActivate: [guestGuard] },
  { path: 'insurance', component: InsuranceAnalyzerComponent, canActivate: [authGuard] },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '' },
];
