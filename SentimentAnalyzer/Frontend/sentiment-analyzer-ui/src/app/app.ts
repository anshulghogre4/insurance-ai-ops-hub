import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Nav } from './components/nav/nav';
import { ThemeService } from './services/theme.service';
import { AuthService } from './services/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, Nav],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  title = 'InsureSense AI - Insurance Sentiment Analyzer';

  // Inject ThemeService to ensure it initializes and applies the saved theme on app boot
  private themeService = inject(ThemeService);
  authService = inject(AuthService);
}
