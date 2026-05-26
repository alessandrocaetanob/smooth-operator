import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';
import {
  ListRecordingsFilters,
  Recording,
  RecordingsService,
  RecordingStatus,
} from '../../services/recordings.service';
import { RecordingPlayerComponent } from './recording-player.component';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-recordings-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RecordingPlayerComponent],
  templateUrl: './recordings.html',
})
export class RecordingsPage implements OnInit {
  private readonly recordings = inject(RecordingsService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly auth = inject(AuthService);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly items = signal<Recording[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(25);

  readonly filterConnectionId = signal('');
  readonly filterVaultId = signal('');
  readonly filterFrom = signal('');
  readonly filterTo = signal('');
  readonly filterStatus = signal<RecordingStatus | ''>('');

  readonly playerOpenFor = signal<Recording | null>(null);

  readonly isOwnerOrAdmin = computed(() => {
    const roles = this.auth.currentUser()?.roles ?? [];
    return roles.includes('Owner') || roles.includes('Admin');
  });

  readonly pageCount = computed(() => {
    const t = this.total();
    const ps = this.pageSize();
    return ps === 0 ? 0 : Math.max(1, Math.ceil(t / ps));
  });

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.error.set(null);
    const filters: ListRecordingsFilters = {
      page: this.page(),
      pageSize: this.pageSize(),
      connectionId: this.filterConnectionId() || null,
      vaultId: this.filterVaultId() || null,
      from: this.filterFrom() || null,
      to: this.filterTo() || null,
      status: this.filterStatus() || null,
    };
    this.recordings.list(filters).subscribe({
      next: (r) => {
        this.loading.set(false);
        this.items.set(r.items);
        this.total.set(r.total);
        this.page.set(r.page);
        this.pageSize.set(r.pageSize);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(this.toMessage(err) || 'Failed to load recordings.');
      },
    });
  }

  applyFilters(): void {
    this.page.set(1);
    this.refresh();
  }

  clearFilters(): void {
    this.filterConnectionId.set('');
    this.filterVaultId.set('');
    this.filterFrom.set('');
    this.filterTo.set('');
    this.filterStatus.set('');
    this.applyFilters();
  }

  nextPage(): void {
    if (this.page() < this.pageCount()) {
      this.page.update((p) => p + 1);
      this.refresh();
    }
  }

  prevPage(): void {
    if (this.page() > 1) {
      this.page.update((p) => p - 1);
      this.refresh();
    }
  }

  open(r: Recording): void {
    if (r.status !== 'Available') return;
    this.playerOpenFor.set(r);
  }

  closePlayer(): void {
    this.playerOpenFor.set(null);
  }

  download(r: Recording): void {
    if (r.status !== 'Available') return;
    const link = document.createElement('a');
    link.href = this.recordings.streamUrl(r.id);
    link.download = `${r.sessionId}.guac`;
    document.body.appendChild(link);
    link.click();
    link.remove();
  }

  async remove(r: Recording): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Delete recording',
      message: `Permanently delete the recording for "${r.connectionName}" started ${r.startedAt}? This cannot be undone.`,
      confirmLabel: 'Delete',
      tone: 'danger',
    });
    if (!ok) return;
    try {
      await firstValueFrom(this.recordings.remove(r.id));
      this.toast.success('Recording deleted.');
      this.refresh();
    } catch (err) {
      this.toast.error(this.toMessage(err) || 'Failed to delete recording.');
    }
  }

  formatBytes(bytes?: number | null): string {
    if (bytes == null) return '—';
    if (bytes < 1024) return `${bytes} B`;
    const units = ['KB', 'MB', 'GB'];
    let v = bytes / 1024;
    let i = 0;
    while (v >= 1024 && i < units.length - 1) {
      v /= 1024;
      i++;
    }
    return `${v.toFixed(1)} ${units[i]}`;
  }

  formatDuration(seconds?: number | null): string {
    if (seconds == null) return '—';
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = seconds % 60;
    if (h > 0) return `${h}h ${m}m`;
    if (m > 0) return `${m}m ${s}s`;
    return `${s}s`;
  }

  statusClass(status: RecordingStatus): string {
    switch (status) {
      case 'Available':
        return 'text-tertiary';
      case 'Recording':
      case 'Uploading':
        return 'text-primary';
      case 'Failed':
        return 'text-error';
      default:
        return 'text-on-surface-variant';
    }
  }

  private toMessage(err: unknown): string {
    if (!err) return '';
    const anyErr = err as { error?: { message?: string }; message?: string };
    return anyErr.error?.message ?? anyErr.message ?? '';
  }
}
