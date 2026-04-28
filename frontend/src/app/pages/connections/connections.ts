import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, Observable, of } from 'rxjs';
import { switchMap, catchError } from 'rxjs/operators';
import {
  Connection,
  ConnectionsService,
  CreateConnectionPayload,
} from '../../services/connections.service';
import { HostsService, AppHost } from '../../services/hosts.service';
import { CredentialsService, Credential } from '../../services/credentials.service';
import { Vault, VaultsService } from '../../services/vaults.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';
import { Mascot, MascotState } from '../../shared/mascot/mascot';

interface FormState {
  id: string | null;
  name: string;
  protocol: string;
  hostId: string;
  connectionGroupId: string;
  credentialId: string;
  settings: string;
  newHostAddress: string;
}

const EMPTY_FORM: FormState = {
  id: null,
  name: '',
  protocol: 'rdp',
  hostId: '',
  connectionGroupId: '',
  credentialId: '',
  settings: '{}',
  newHostAddress: '',
};

@Component({
  selector: 'app-connections',
  imports: [FormsModule, Mascot],
  templateUrl: './connections.html',
  styleUrl: './connections.css',
})
export class Connections implements OnInit {
  private readonly connectionsSvc = inject(ConnectionsService);
  private readonly hostsSvc = inject(HostsService);
  private readonly credentialsSvc = inject(CredentialsService);
  private readonly vaultsSvc = inject(VaultsService);
  private readonly confirmSvc = inject(ConfirmDialogService);
  private readonly toastSvc = inject(ToastService);

  readonly connections = this.connectionsSvc.list;
  readonly hosts = this.hostsSvc.list;
  readonly credentials = this.credentialsSvc.list;
  readonly vaults = this.vaultsSvc.list;

  // Performance optimization: O(1) lookups for template bindings
  readonly hostsMap = computed(() => new Map(this.hosts().map((h) => [h.id, h])));
  readonly credentialsMap = computed(() => new Map(this.credentials().map((c) => [c.id, c])));
  readonly vaultsMap = computed(() => new Map(this.vaults().map((v) => [v.id, v])));

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showForm = signal(false);
  readonly form = signal<FormState>({ ...EMPTY_FORM });
  readonly busy = signal(false);
  readonly mascotState = signal<MascotState>('idle');

  readonly protocols = ['rdp', 'ssh', 'vnc'];

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      conns: this.connectionsSvc.reload(),
      hosts: this.hostsSvc.reload(),
      creds: this.credentialsSvc.reload(),
      vaults: this.vaultsSvc.reload(),
    }).subscribe({
      next: () => this.loading.set(false),
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(this.toMessage(err) || 'Failed to load.');
      },
    });
  }

  newConnection(): void {
    this.form.set({ ...EMPTY_FORM });
    this.showForm.set(true);
  }

  edit(c: Connection): void {
    this.form.set({
      id: c.id,
      name: c.name,
      protocol: c.protocol,
      hostId: c.hostId,
      connectionGroupId: c.connectionGroupId ?? '',
      credentialId: c.credentialId ?? '',
      settings: c.settings || '{}',
      newHostAddress: '',
    });
    this.showForm.set(true);
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.set({ ...EMPTY_FORM });
  }

  patch<K extends keyof FormState>(key: K, value: FormState[K]): void {
    this.form.update((f) => ({ ...f, [key]: value }));
  }

  save(): void {
    if (this.busy()) return;
    const f = this.form();

    // We need either an existing hostId or a new host address
    if (
      !f.name.trim() ||
      (!f.hostId && !f.newHostAddress.trim()) ||
      !f.protocol ||
      !f.connectionGroupId
    ) {
      this.errorMessage.set('Name, host, protocol and vault are required.');
      this.mascotState.set('error');
      return;
    }

    this.busy.set(true);
    this.errorMessage.set(null);
    this.mascotState.set('thinking');

    // Helper to determine host ID stream
    const getHostId$ = (): Observable<string> => {
      if (f.hostId) {
        return of(f.hostId);
      } else {
        // Create new host inline
        const newHostName = f.name.trim() + ' Host'; // Fallback name
        return this.hostsSvc.create({ name: newHostName, address: f.newHostAddress.trim() }).pipe(
          switchMap(() => this.hostsSvc.reload()), // Reload to get the new host in state
          switchMap((hosts) => {
            const created = hosts.find((h) => h.address === f.newHostAddress.trim());
            if (!created) throw new Error('Failed to locate created host');
            return of(created.id);
          }),
        );
      }
    };

    getHostId$()
      .pipe(
        switchMap((resolvedHostId) => {
          const payload: CreateConnectionPayload = {
            name: f.name.trim(),
            protocol: f.protocol,
            hostId: resolvedHostId,
            connectionGroupId: f.connectionGroupId || null,
            credentialId: f.credentialId || null,
            settings: f.settings || '{}',
          };

          return f.id
            ? this.connectionsSvc.update(f.id, payload)
            : this.connectionsSvc.create(payload);
        }),
        catchError((err) => {
          throw err;
        }),
      )
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.mascotState.set('success');
          this.toastSvc.success(f.id ? 'Connection updated.' : 'Connection created.');
          setTimeout(() => this.cancel(), 1000); // Give time to see success state
          this.refresh();
        },
        error: (err: any) => {
          this.busy.set(false);
          this.mascotState.set('error');
          const msg = this.toMessage(err) || 'Save failed.';
          this.errorMessage.set(msg);
          this.toastSvc.error(msg);
        },
      });
  }

  async remove(c: Connection): Promise<void> {
    const ok = await this.confirmSvc.ask({
      title: 'Delete connection',
      message: `Delete connection "${c.name}"? This cannot be undone.`,
      confirmLabel: 'Delete',
      tone: 'danger',
    });
    if (!ok) return;
    this.errorMessage.set(null);
    this.connectionsSvc.remove(c.id).subscribe({
      next: () => {
        this.toastSvc.success(`Connection "${c.name}" deleted.`);
        this.refresh();
      },
      error: (err) => {
        const msg = this.toMessage(err) || 'Delete failed.';
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
      },
    });
  }

  trackById(_: number, c: Connection): string {
    return c.id;
  }

  trackHost(_: number, h: AppHost): string {
    return h.id;
  }

  trackCred(_: number, c: Credential): string {
    return c.id;
  }

  trackVault(_: number, v: Vault): string {
    return v.id;
  }

  hostName(id: string): string {
    return this.hostsMap().get(id)?.name ?? '—';
  }

  credentialName(id: string | null | undefined): string {
    if (!id) return '—';
    return this.credentialsMap().get(id)?.name ?? '—';
  }

  vaultName(id: string | null | undefined): string {
    if (!id) return '—';
    return this.vaultsMap().get(id)?.name ?? '—';
  }

  private toMessage(err: any): string | null {
    return err?.error?.message ?? err?.error?.Message ?? err?.message ?? null;
  }
}
