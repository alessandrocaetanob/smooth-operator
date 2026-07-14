import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
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
  labelKey: string;
}

type StatusTone = 'ok' | 'bad' | 'idle';

@Component({
  selector: 'app-webhooks-settings',
  imports: [FormsModule, DatePipe, TranslatePipe],
  templateUrl: './webhooks.html',
  styleUrl: './webhooks.css',
})
export class WebhooksSettings implements OnInit {
  private readonly svc = inject(WebhooksService);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly translate = inject(TranslateService);

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
    { pattern: 'user.*', labelKey: 'pages.settingsWebhooks.events.users' },
    { pattern: 'connection.*', labelKey: 'pages.settingsWebhooks.events.connections' },
    { pattern: 'credential.*', labelKey: 'pages.settingsWebhooks.events.credentials' },
    { pattern: 'sso.*', labelKey: 'pages.settingsWebhooks.events.sso' },
    { pattern: 'group.*', labelKey: 'pages.settingsWebhooks.events.groups' },
    { pattern: 'invite.*', labelKey: 'pages.settingsWebhooks.events.invitations' },
    { pattern: 'webhook.*', labelKey: 'pages.settingsWebhooks.events.webhooks' },
    { pattern: 'system.*', labelKey: 'pages.settingsWebhooks.events.system' },
  ];

  readonly allEventsSelected = computed(() => this.selectedEvents().includes('*'));
  readonly editorTitle = computed(() =>
    this.editingId()
      ? 'pages.settingsWebhooks.editor.titleEdit'
      : 'pages.settingsWebhooks.editor.titleNew',
  );

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.svc.load().subscribe({
      next: () => this.loading.set(false),
      error: (err) => {
        this.loading.set(false);
        this.error.set(
          this.toMessage(err) || this.translate.instant('pages.settingsWebhooks.errors.loadFailed'),
        );
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
      this.error.set(this.translate.instant('pages.settingsWebhooks.errors.nameUrlRequired'));
      return;
    }
    const eventTypes = this.selectedEvents().join(',');
    this.busy.set(true);
    this.clearFeedback();

    const id = this.editingId();
    if (id) {
      const payload: UpdateWebhookPayload = { name, url, eventTypes, enabled: this.formEnabled() };
      this.svc.update(id, payload).subscribe({
        next: () =>
          this.afterWrite(this.translate.instant('pages.settingsWebhooks.messages.updated')),
        error: (err) =>
          this.failWrite(err, this.translate.instant('pages.settingsWebhooks.errors.saveFailed')),
      });
    } else {
      const payload: CreateWebhookPayload = { name, url, eventTypes };
      this.svc.create(payload).subscribe({
        next: (secret) => {
          this.revealedSecret.set(secret);
          this.afterWrite(this.translate.instant('pages.settingsWebhooks.messages.created'));
        },
        error: (err) =>
          this.failWrite(err, this.translate.instant('pages.settingsWebhooks.errors.createFailed')),
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
        this.message.set(
          this.translate.instant(
            w.enabled
              ? 'pages.settingsWebhooks.messages.disabled'
              : 'pages.settingsWebhooks.messages.enabled',
          ),
        );
        this.svc.load().subscribe();
      },
      error: (err) => {
        this.rowBusyId.set(null);
        this.error.set(
          this.toMessage(err) ||
            this.translate.instant('pages.settingsWebhooks.errors.updateFailed'),
        );
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
        this.message.set(
          this.translate.instant('pages.settingsWebhooks.messages.testQueued', { name: w.name }),
        );
      },
      error: (err) => {
        this.rowBusyId.set(null);
        this.error.set(
          this.toMessage(err) || this.translate.instant('pages.settingsWebhooks.errors.testFailed'),
        );
      },
    });
  }

  async requestDelete(w: Webhook): Promise<void> {
    this.closeMenu();
    const confirmed = await this.confirm.ask({
      title: this.translate.instant('pages.settingsWebhooks.confirmDelete.title'),
      message: this.translate.instant('pages.settingsWebhooks.confirmDelete.message', {
        name: w.name,
      }),
      confirmLabel: this.translate.instant('common.actions.delete'),
      tone: 'danger',
    });
    if (confirmed) this.doDelete(w.id);
  }

  async requestRotate(w: Webhook): Promise<void> {
    this.closeMenu();
    const confirmed = await this.confirm.ask({
      title: this.translate.instant('pages.settingsWebhooks.confirmRotate.title'),
      message: this.translate.instant('pages.settingsWebhooks.confirmRotate.message', {
        name: w.name,
      }),
      confirmLabel: this.translate.instant('pages.settingsWebhooks.confirmRotate.confirmLabel'),
    });
    if (confirmed) this.doRotate(w.id);
  }

  private doDelete(id: string): void {
    this.rowBusyId.set(id);
    this.clearFeedback();
    this.svc.remove(id).subscribe({
      next: () => {
        this.rowBusyId.set(null);
        this.message.set(this.translate.instant('pages.settingsWebhooks.messages.deleted'));
        this.svc.load().subscribe();
      },
      error: (err) => {
        this.rowBusyId.set(null);
        this.error.set(
          this.toMessage(err) ||
            this.translate.instant('pages.settingsWebhooks.errors.deleteFailed'),
        );
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
        this.message.set(this.translate.instant('pages.settingsWebhooks.messages.rotated'));
      },
      error: (err) => {
        this.rowBusyId.set(null);
        this.error.set(
          this.toMessage(err) ||
            this.translate.instant('pages.settingsWebhooks.errors.rotateFailed'),
        );
      },
    });
  }

  // --- One-time secret banner ---

  copySecret(): void {
    const s = this.revealedSecret();
    if (!s) return;
    navigator.clipboard?.writeText(s.secret).then(
      () =>
        this.message.set(this.translate.instant('pages.settingsWebhooks.messages.secretCopied')),
      () => this.error.set(this.translate.instant('pages.settingsWebhooks.errors.copyFailed')),
    );
  }

  dismissSecret(): void {
    this.revealedSecret.set(null);
  }

  copyUrl(w: Webhook): void {
    navigator.clipboard?.writeText(w.url).then(
      () => this.message.set(this.translate.instant('pages.settingsWebhooks.messages.urlCopied')),
      () => this.error.set(this.translate.instant('pages.settingsWebhooks.errors.copyFailed')),
    );
  }

  // --- Display helpers ---

  eventSummary(eventTypes: string): string {
    const parts = this.parseEventTypes(eventTypes);
    if (parts.includes('*')) return this.translate.instant('pages.settingsWebhooks.events.all');
    return parts
      .map((p) => {
        const labelKey = this.eventCategories.find((c) => c.pattern === p)?.labelKey;
        return labelKey ? this.translate.instant(labelKey) : p;
      })
      .join(', ');
  }

  statusText(w: Webhook): string {
    if (!w.enabled) return this.translate.instant('pages.settingsWebhooks.status.disabled');
    if (!w.lastDeliveryStatus)
      return this.translate.instant('pages.settingsWebhooks.status.noDeliveries');
    if (w.lastDeliveryStatus === 'success')
      return this.translate.instant('pages.settingsWebhooks.status.healthy');
    return this.translate.instant('pages.settingsWebhooks.status.failing', {
      count: w.consecutiveFailures,
    });
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
