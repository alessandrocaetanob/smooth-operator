import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, Observable } from 'rxjs';
import {
  Connection,
  ConnectionsService,
  CreateConnectionPayload,
} from '../../services/connections.service';
import { HostsService, AppHost } from '../../services/hosts.service';
import { CredentialsService, Credential } from '../../services/credentials.service';

interface FormState {
  id: string | null;
  name: string;
  protocol: string;
  hostId: string;
  credentialId: string;
  settings: string;
}

const EMPTY_FORM: FormState = {
  id: null,
  name: '',
  protocol: 'rdp',
  hostId: '',
  credentialId: '',
  settings: '{}',
};

@Component({
  selector: 'app-connections',
  imports: [FormsModule],
  templateUrl: './connections.html',
  styleUrl: './connections.css',
})
export class Connections implements OnInit {
  private readonly connectionsSvc = inject(ConnectionsService);
  private readonly hostsSvc = inject(HostsService);
  private readonly credentialsSvc = inject(CredentialsService);

  readonly connections = this.connectionsSvc.list;
  readonly hosts = this.hostsSvc.list;
  readonly credentials = this.credentialsSvc.list;

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showForm = signal(false);
  readonly form = signal<FormState>({ ...EMPTY_FORM });
  readonly busy = signal(false);

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
      credentialId: c.credentialId ?? '',
      settings: c.settings || '{}',
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
    if (!f.name.trim() || !f.hostId || !f.protocol) {
      this.errorMessage.set('Name, host and protocol are required.');
      return;
    }
    const payload: CreateConnectionPayload = {
      name: f.name.trim(),
      protocol: f.protocol,
      hostId: f.hostId,
      credentialId: f.credentialId || null,
      settings: f.settings || '{}',
    };
    this.busy.set(true);
    this.errorMessage.set(null);
    const obs: Observable<unknown> = f.id
      ? this.connectionsSvc.update(f.id, payload)
      : this.connectionsSvc.create(payload);
    obs.subscribe({
      next: () => {
        this.busy.set(false);
        this.cancel();
        this.refresh();
      },
      error: (err: any) => {
        this.busy.set(false);
        this.errorMessage.set(this.toMessage(err) || 'Save failed.');
      },
    });
  }

  remove(c: Connection): void {
    if (!confirm(`Delete connection "${c.name}"?`)) return;
    this.errorMessage.set(null);
    this.connectionsSvc.remove(c.id).subscribe({
      next: () => this.refresh(),
      error: (err) => this.errorMessage.set(this.toMessage(err) || 'Delete failed.'),
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

  hostName(id: string): string {
    return this.hosts().find((h) => h.id === id)?.name ?? '—';
  }

  credentialName(id: string | null | undefined): string {
    if (!id) return '—';
    return this.credentials().find((c) => c.id === id)?.name ?? '—';
  }

  private toMessage(err: any): string | null {
    return err?.error?.message ?? err?.error?.Message ?? err?.message ?? null;
  }
}
