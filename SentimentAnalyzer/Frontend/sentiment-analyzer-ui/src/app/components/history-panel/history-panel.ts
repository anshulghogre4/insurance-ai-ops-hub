import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AnalysisHistoryItem } from '../../models/insurance.model';

@Component({
  selector: 'app-history-panel',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './history-panel.html',
  styleUrl: './history-panel.css'
})
export class HistoryPanelComponent {
  items = input<AnalysisHistoryItem[]>([]);
  loading = input(false);

  selectItem = output<AnalysisHistoryItem>();
  close = output<void>();

  onSelect(item: AnalysisHistoryItem): void {
    this.selectItem.emit(item);
  }

  onClose(): void {
    this.close.emit();
  }

  getSentimentBadgeClass(sentiment: string): string {
    const raw = sentiment?.toLowerCase() || '';
    if (['positive', 'happy', 'satisfied', 'pleased', 'grateful', 'delighted', 'content', 'impressed'].includes(raw)) return 'badge-success';
    if (['negative', 'angry', 'frustrated', 'upset', 'furious', 'dissatisfied', 'annoyed', 'hostile', 'bitter'].includes(raw)) return 'badge-danger';
    if (['mixed', 'ambivalent', 'conflicted'].includes(raw)) return 'badge-warning';
    return 'badge-info';
  }

  getChurnBadgeClass(risk: string): string {
    switch (risk?.toLowerCase()) {
      case 'high': return 'badge-danger';
      case 'medium': return 'badge-warning';
      default: return 'badge-success';
    }
  }

  formatRelativeTime(dateStr: string): string {
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    const diffDays = Math.floor(diffHours / 24);
    if (diffDays < 7) return `${diffDays}d ago`;
    return date.toLocaleDateString();
  }

  truncateText(text: string, maxLength: number = 80): string {
    if (!text || text.length <= maxLength) return text;
    return text.substring(0, maxLength) + '...';
  }
}
