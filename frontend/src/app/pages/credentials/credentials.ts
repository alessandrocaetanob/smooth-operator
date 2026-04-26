import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Credential,
  CredentialsService,
  CreateCredentialPayload,
  UpdateCredentialPayload,
} from '../../services/credentials.service';
import { AuthService } from '../../services/auth.service';

interface FormState {
  id: string | null;
  name: string;
  username: string;
  secret: string;
  credentialType: string;
}

const EMPTY_FORM: FormState = {
  id: null,
  name: '',
  username: '',
  secret: '',
  credentialType: 'password',
};

@Component({
  selector: 'app-credentials',
  imports: [FormsModule],
  templateUrl: './credentials.html',
  styleUrl: './credentials.css',
})
export class Credentials implements OnInit {
  private readonly svc = inject(CredentialsService);
  private readonly auth = inject(AuthService);

  readonly credentials = this.svc.list;
  readonly canManageCredentials = this.auth.canManageCredentials;
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showForm = signal(false);
  readonly form = signal<FormState>({ ...EMPTY_FORM });
  readonly busy = signal(false);

  readonly types = [
    { value: 'password', label: 'Password' },
    { value: 'private_key', label: 'Private key (SSH)' },
    { value: 'api_token', label: 'API token' },
  ];

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.svc.reload().subscribe({
      next: () => this.loading.set(false),
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(this.toMessage(err) || 'Failed to load.');
      },
    });
  }

  newCredential(): void {
    if (!this.canManageCredentials()) return;
    this.form.set({ ...EMPTY_FORM });
    this.showForm.set(true);
  }

  edit(c: Credential): void {
    if (!this.canManageCredentials()) return;
    this.form.set({
      id: c.id,
      name: c.name,
      username: c.username,
      secret: '',
      credentialType: c.credentialType,
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
    if (!this.canManageCredentials()) return;
    if (this.busy()) return;
    const f = this.form();
    if (!f.name.trim() || !f.username.trim()) {
      this.errorMessage.set('Name and username are required.');
      return;
    }
    if (!f.id && !f.secret) {
      this.errorMessage.set('Secret is required when creating a credential.');
      return;
    }
    this.busy.set(true);
    this.errorMessage.set(null);
    if (f.id) {
      const upd: UpdateCredentialPayload = {
        name: f.name.trim(),
        username: f.username.trim(),
        credentialType: f.credentialType,
      };
      if (f.secret) upd.secret = f.secret;
      this.svc.update(f.id, upd).subscribe({
        next: () => this.done(),
        error: (err) => this.fail(err),
      });
    } else {
      const create: CreateCredentialPayload = {
        name: f.name.trim(),
        username: f.username.trim(),
        secret: f.secret,
        credentialType: f.credentialType,
      };
      this.svc.create(create).subscribe({
        next: () => this.done(),
        error: (err) => this.fail(err),
      });
    }
  }

  remove(c: Credential): void {
    if (!this.canManageCredentials()) return;
    if (!confirm(`Delete credential "${c.name}"?`)) return;
    this.errorMessage.set(null);
    this.svc.remove(c.id).subscribe({
      next: () => this.refresh(),
      error: (err) => this.errorMessage.set(this.toMessage(err) || 'Delete failed.'),
    });
  }

  trackById(_: number, c: Credential): string {
    return c.id;
  }

  private done(): void {
    this.busy.set(false);
    this.cancel();
    this.refresh();
  }

  private fail(err: any): void {
    this.busy.set(false);
    this.errorMessage.set(this.toMessage(err) || 'Save failed.');
  }

  private toMessage(err: any): string | null {
    return err?.error?.message ?? err?.error?.Message ?? err?.message ?? null;
  }
}
