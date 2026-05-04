import { DatePipe, SlicePipe } from '@angular/common';
import { Component, ElementRef, OnInit, ViewChild, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  AuditLogEntry,
  AuditLogQuery,
  AuditLogsService,
} from '../../../services/audit-logs.service';

@Component({
  selector: 'app-audit-logs',
  imports: [FormsModule, DatePipe, SlicePipe],
  templateUrl: './audit-logs.html',
  styleUrl: './audit-logs.css',
})
export class AuditLogs implements OnInit {
  private readonly svc = inject(AuditLogsService);

  @ViewChild('closeBtn') closeBtnRef?: ElementRef<HTMLButtonElement>;

  readonly result = this.svc.result;
  readonly loading = signal(false);

  readonly selectedEntry = signal<AuditLogEntry | null>(null);

  constructor() {
    effect(() => {
      if (this.selectedEntry()) {
        // Move focus into the panel so Escape keydown reaches the aside handler
        setTimeout(() => this.closeBtnRef?.nativeElement.focus(), 0);
      }
    });
  }

  selectEntry(entry: AuditLogEntry): void {
    this.selectedEntry.set(entry);
  }

  closeDetail(): void {
    this.selectedEntry.set(null);
  }

  actionCategory(action: string): string {
    if (!action) return 'action-default';
    const a = action.toLowerCase();
    if (a.includes('fail') || a.includes('error') || a.includes('denied')) return 'action-error';
    if (a.startsWith('connection.ticket')) return 'action-ticket';
    if (a.startsWith('connection.')) return 'action-connection';
    if (a.startsWith('user.')) return 'action-user';
    if (a.startsWith('invite.')) return 'action-invite';
    if (a.startsWith('system.')) return 'action-system';
    return 'action-default';
  }

  outcomeBadgeClass(outcome: string): string {
    return outcome === 'failure' ? 'outcome-badge-failure' : 'outcome-badge-success';
  }

  formattedDetails(details: string): string {
    try {
      const parsed = JSON.parse(details);
      if (Object.keys(parsed).length === 0) return '(no details)';
      return JSON.stringify(parsed, null, 2);
    } catch {
      return details || '(no details)';
    }
  }

  readonly user = signal('');
  readonly action = signal('');
  readonly resourceType = signal('');
  readonly from = signal('');
  readonly to = signal('');
  readonly outcome = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(25);

  ngOnInit(): void {
    this.search();
  }

  search(): void {
    this.page.set(1);
    this.load();
  }

  goToPage(p: number): void {
    if (p < 1 || (this.result().totalPages > 0 && p > this.result().totalPages)) return;
    this.page.set(p);
    this.load();
  }

  reset(): void {
    this.user.set('');
    this.action.set('');
    this.resourceType.set('');
    this.from.set('');
    this.to.set('');
    this.outcome.set('');
    this.search();
  }

  exportCsv(): void {
    const url = this.svc.exportCsvUrl(this.buildQuery(false));
    // Open in a new tab — interceptor adds Authorization header for /api/ XHRs,
    // but a download URL must be authenticated server-side. Since cookies aren't
    // used, we instead fetch with the auth header and trigger a blob download.
    fetch(url, { headers: this.authHeaders() })
      .then((r) => r.blob())
      .then((blob) => {
        const a = document.createElement('a');
        const objectUrl = URL.createObjectURL(blob);
        a.href = objectUrl;
        a.download = 'audit-logs.csv';
        a.click();
        URL.revokeObjectURL(objectUrl);
      });
  }

  private authHeaders(): HeadersInit {
    const t = localStorage.getItem('smooth-operator.token');
    return t ? { Authorization: `Bearer ${t}` } : {};
  }

  setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
    this.load();
  }

  onPageSizeChange(value: string | number): void {
    const n = typeof value === 'number' ? value : parseInt(value, 10);
    if (!Number.isFinite(n) || n < 1) return;
    this.setPageSize(n);
  }

  private load(): void {
    this.loading.set(true);
    this.svc.reload(this.buildQuery(true)).subscribe({
      next: () => this.loading.set(false),
      error: () => this.loading.set(false),
    });
  }

  private buildQuery(withPaging: boolean): AuditLogQuery {
    const q: AuditLogQuery = {};
    if (withPaging) {
      q.page = this.page();
      q.pageSize = this.pageSize();
    }
    if (this.user().trim()) q.user = this.user().trim();
    if (this.action().trim()) q.action = this.action().trim();
    if (this.resourceType().trim()) q.resourceType = this.resourceType().trim();
    if (this.from()) q.from = new Date(this.from()).toISOString();
    if (this.to()) q.to = new Date(this.to()).toISOString();
    if (this.outcome()) q.outcome = this.outcome();
    return q;
  }
}
