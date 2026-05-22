import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import {
  CreateWebhookPayload,
  UpdateWebhookPayload,
  Webhook,
  WebhookSecret,
  WebhooksService,
} from '../../../services/webhooks.service';
import { ConfirmDialogService } from '../../../shared/confirm-dialog/confirm-dialog.service';

interface EventCategory {
  pattern: string;
  label: string;
}

type StatusTone = 'ok' | 'bad' | 'idle';

@Component({
  selector: 'app-webhooks-settings',
  imports: [FormsModule, DatePipe],
  templateUrl: './webhooks.html',
  styleUrl: './webhooks.css',
})
export class WebhooksSettings implements OnInit {
  private readonly svc = inject(WebhooksService);
  private readonly confirm = inject(ConfirmDialogService);

  readonly webhooks = this.svc.list;
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  /** When set, the one-time signing secret banner is shown. */
  readonly revealedSecret = signal<WebhookSecret | null>(null);

  // --- Inline create/edit editor ---
  readonly editorOpen = signal(false);
  /** null while creating, the endpoint id while editing. */
  readonly editingId = signal<string | null>(null);
  readonly formName = signal('');
  readonly formUrl = signal('');
  readonly formEnabled = signal(true);
  /** Selected event-type patterns; `['*']` means all events. */
  readonly selectedEvents = signal<string[]>(['*']);

  // --- Per-row transient state ---
  readonly rowBusyId = signal<string | null>(null);
  /** Endpoint id whose overflow menu is open. */
  readonly openMenuId = signal<string | null>(null);

  readonly eventCategories: EventCategory[] = [
    { pattern: 'user.*', label: 'Users & authentication' },
    { pattern: 'connection.*', label: 'Connections' },
    { pattern: 'credential.*', label: 'Credentials' },
    { pattern: 'sso.*', label: 'Single sign-on' },
    { pattern: 'group.*', label: 'Groups' },
    { pattern: 'invite.*', label: 'Invitations' },
    { pattern: 'webhook.*', label: 'Webhooks' },
    { pattern: 'system.*', label: 'System' },
  ];

  readonly allEventsSelected = computed(() => this.selectedEvents().includes('*'));
  readonly editorTitle = computed(() => (this.editingId() ? 'Edit endpoint' : 'New endpoint'));

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.svc.load().subscribe({
      next: () => this.loading.set(false),
      error: (err) => {
        this.loading.set(false);
        this.error.set(this.toMessage(err) || 'Failed to load webhooks.');
      },
    });
  }

  // --- Overflow menu ---

  toggleMenu(id: string): void {
    this.openMenuId.update((current) => (current === id ? null : id));
  }

  closeMenu(): void {
    this.openMenuId.set(null);
  }

  // --- Editor ---

  openCreate(): void {
    this.editingId.set(null);
    this.formName.set('');
    this.formUrl.set('');
    this.formEnabled.set(true);
    this.selectedEvents.set(['*']);
    this.clearFeedback();
    this.editorOpen.set(true);
  }

  openEdit(w: Webhook): void {
    this.closeMenu();
    this.editingId.set(w.id);
    this.formName.set(w.name);
    this.formUrl.set(w.url);
    this.formEnabled.set(w.enabled);
    this.selectedEvents.set(this.parseEventTypes(w.eventTypes));
    this.clearFeedback();
    this.editorOpen.set(true);
  }

  closeEditor(): void {
    this.editorOpen.set(false);
  }

  selectAllEvents(): void {
    this.selectedEvents.set(['*']);
  }

  toggleCategory(pattern: string): void {
    const current = this.selectedEvents().filter((p) => p !== '*');
    const next = current.includes(pattern)
      ? current.filter((p) => p !== pattern)
      : [...current, pattern];
    // An empty selection is meaningless — fall back to "all events".
    this.selectedEvents.set(next.length > 0 ? next : ['*']);
  }

  isCategorySelected(pattern: string): boolean {
    return this.selectedEvents().includes(pattern);
  }

  submitEditor(): void {
    if (this.busy()) return;
    const name = this.formName().trim();
    const url = this.formUrl().trim();
    if (!name || !url) {
      this.error.set('Name and URL are required.');
      return;
    }
    const eventTypes = this.selectedEvents().join(',');
    this.busy.set(true);
    this.clearFeedback();

    const id = this.editingId();
    if (id) {
      const payload: UpdateWebhookPayload = { name, url, eventTypes, enabled: this.formEnabled() };
      this.svc.update(id, payload).subscribe({
        next: () => this.afterWrite('Webhook endpoint updated.'),
        error: (err) => this.failWrite(err, 'Failed to save webhook.'),
      });
    } else {
      const payload: CreateWebhookPayload = { name, url, eventTypes };
      this.svc.create(payload).subscribe({
        next: (secret) => {
          this.revealedSecret.set(secret);
          this.afterWrite('Webhook endpoint created.');
        },
        error: (err) => this.failWrite(err, 'Failed to create webhook.'),
      });
    }
  }

  // --- Row actions ---

  toggleEnabled(w: Webhook): void {
    this.closeMenu();
    if (this.rowBusyId()) return;
    this.rowBusyId.set(w.id);
    this.clearFeedback();
    const payload: UpdateWebhookPayload = {
      name: w.name,
      url: w.url,
      eventTypes: w.eventTypes,
      enabled: !w.enabled,
    };
    this.svc.update(w.id, payload).subscribe({
      next: () => {
        this.rowBusyId.set(null);
        this.message.set(w.enabled ? 'Webhook disabled.' : 'Webhook enabled.');
        this.svc.load().subscribe();
      },
      error: (err) => {
        this.rowBusyId.set(null);
        this.error.set(this.toMessage(err) || 'Failed to update webhook.');
      },
    });
  }

  sendTest(w: Webhook): void {
    if (this.rowBusyId()) return;
    this.rowBusyId.set(w.id);
    this.clearFeedback();
    this.svc.sendTest(w.id).subscribe({
      next: () => {
        this.rowBusyId.set(null);
        this.message.set(`Test event queued for "${w.name}". It will be delivered shortly.`);
      },
      error: (err) => {
        this.rowBusyId.set(null);
        this.error.set(this.toMessage(err) || 'Failed to queue test event.');
      },
    });
  }

  async requestDelete(w: Webhook): Promise<void> {
    this.closeMenu();
    const confirmed = await this.confirm.ask({
      title: 'Delete webhook endpoint?',
      message: `"${w.name}" will be removed and stop receiving events. This cannot be undone.`,
      confirmLabel: 'Delete',
      tone: 'danger',
    });
    if (confirmed) this.doDelete(w.id);
  }

  async requestRotate(w: Webhook): Promise<void> {
    this.closeMenu();
    const confirmed = await this.confirm.ask({
      title: 'Rotate signing secret?',
      message: `The current secret for "${w.name}" stops working immediately — update your receiver with the new secret right away.`,
      confirmLabel: 'Rotate secret',
    });
    if (confirmed) this.doRotate(w.id);
  }

  private doDelete(id: string): void {
    this.rowBusyId.set(id);
    this.clearFeedback();
    this.svc.remove(id).subscribe({
      next: () => {
        this.rowBusyId.set(null);
        this.message.set('Webhook endpoint deleted.');
        this.svc.load().subscribe();
      },
      error: (err) => {
        this.rowBusyId.set(null);
        this.error.set(this.toMessage(err) || 'Failed to delete webhook.');
      },
    });
  }

  private doRotate(id: string): void {
    this.rowBusyId.set(id);
    this.clearFeedback();
    this.svc.rotateSecret(id).subscribe({
      next: (secret) => {
        this.rowBusyId.set(null);
        this.revealedSecret.set(secret);
        this.message.set('Signing secret rotated.');
      },
      error: (err) => {
        this.rowBusyId.set(null);
        this.error.set(this.toMessage(err) || 'Failed to rotate secret.');
      },
    });
  }

  // --- One-time secret banner ---

  copySecret(): void {
    const s = this.revealedSecret();
    if (!s) return;
    navigator.clipboard?.writeText(s.secret).then(
      () => this.message.set('Signing secret copied to clipboard.'),
      () => this.error.set('Could not copy to clipboard.'),
    );
  }

  dismissSecret(): void {
    this.revealedSecret.set(null);
  }

  copyUrl(w: Webhook): void {
    navigator.clipboard?.writeText(w.url).then(
      () => this.message.set('Endpoint URL copied to clipboard.'),
      () => this.error.set('Could not copy to clipboard.'),
    );
  }

  // --- Display helpers ---

  eventSummary(eventTypes: string): string {
    const parts = this.parseEventTypes(eventTypes);
    if (parts.includes('*')) return 'All events';
    return parts
      .map((p) => this.eventCategories.find((c) => c.pattern === p)?.label ?? p)
      .join(', ');
  }

  statusText(w: Webhook): string {
    if (!w.enabled) return 'Disabled';
    if (!w.lastDeliveryStatus) return 'No deliveries yet';
    if (w.lastDeliveryStatus === 'success') return 'Healthy';
    return `Failing (${w.consecutiveFailures})`;
  }

  statusTone(w: Webhook): StatusTone {
    if (!w.enabled || !w.lastDeliveryStatus) return 'idle';
    return w.lastDeliveryStatus === 'success' ? 'ok' : 'bad';
  }

  private parseEventTypes(raw: string): string[] {
    const parts = (raw ?? '')
      .split(',')
      .map((p) => p.trim())
      .filter((p) => p.length > 0);
    if (parts.length === 0 || parts.includes('*')) return ['*'];
    return parts;
  }

  private afterWrite(msg: string): void {
    this.busy.set(false);
    this.editorOpen.set(false);
    this.message.set(msg);
    this.svc.load().subscribe();
  }

  private failWrite(err: unknown, fallback: string): void {
    this.busy.set(false);
    this.error.set(this.toMessage(err) || fallback);
  }

  private clearFeedback(): void {
    this.message.set(null);
    this.error.set(null);
  }

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
