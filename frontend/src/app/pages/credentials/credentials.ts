import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Credential,
  CredentialsService,
  CreateCredentialPayload,
  UpdateCredentialPayload,
} from '../../services/credentials.service';
import { AuthService } from '../../services/auth.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';
import { Drawer } from '../../shared/drawer/drawer';

interface FormState {
  id: string | null;
  name: string;
  username: string;
  secret: string;
  credentialType: string;
  publicKey?: string;
}

const EMPTY_FORM: FormState = {
  id: null,
  name: '',
  username: '',
  secret: '',
  credentialType: 'password',
  publicKey: '',
};

@Component({
  selector: 'app-credentials',
  imports: [FormsModule, Drawer],
  templateUrl: './credentials.html',
  styleUrl: './credentials.css',
})
export class Credentials implements OnInit {
  private readonly svc = inject(CredentialsService);
  private readonly auth = inject(AuthService);
  private readonly confirmSvc = inject(ConfirmDialogService);
  private readonly toastSvc = inject(ToastService);

  readonly credentials = this.svc.list;
  readonly canManageCredentials = this.auth.canManageCredentials;
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showDrawer = signal(false);
  readonly form = signal<FormState>({ ...EMPTY_FORM });
  readonly busy = signal(false);
  readonly generatingSsh = signal(false);
  readonly showPublicKey = signal(false);
  readonly generatedPublicKey = signal<string>('');
  readonly sshKeyAlgorithm = signal<'rsa' | 'ecdsa'>('rsa');
  readonly searchQuery = signal('');

  readonly filteredCredentials = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    if (!q) return this.credentials();
    return this.credentials().filter(
      (c) =>
        c.name.toLowerCase().includes(q) ||
        c.username.toLowerCase().includes(q) ||
        c.credentialType.toLowerCase().includes(q),
    );
  });

  readonly isFiltered = computed(() => this.searchQuery().trim().length > 0);

  readonly types = [
    { value: 'password', label: 'Password' },
    { value: 'private_key', label: 'Private key (SSH)' },
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
    this.errorMessage.set(null);
    this.showDrawer.set(true);
  }

  edit(c: Credential): void {
    if (!this.canManageCredentials()) return;
    this.form.set({
      id: c.id,
      name: c.name,
      username: c.username,
      secret: '',
      credentialType: c.credentialType,
      publicKey: c.publicKey || '',
    });
    this.errorMessage.set(null);
    this.showDrawer.set(true);
  }

  generateSshKey(): void {
    if (!this.canManageCredentials()) return;
    this.generatingSsh.set(true);
    this.errorMessage.set(null);
    this.svc.generateSsh(this.sshKeyAlgorithm()).subscribe({
      next: (res) => {
        this.generatingSsh.set(false);
        this.patch('secret', res.privateKey);
        this.patch('publicKey', res.publicKey);
        this.generatedPublicKey.set(res.publicKey);
        this.showPublicKey.set(true);
      },
      error: (err) => {
        this.generatingSsh.set(false);
        this.errorMessage.set(this.toMessage(err) || 'Failed to generate SSH key.');
      },
    });
  }

  copyPublicKey(): void {
    const key = this.publicKeyToShow();
    if (!key) return;
    navigator.clipboard.writeText(key);
    this.toastSvc.success('Public key copied to clipboard');
  }

  copyRowPublicKey(c: Credential): void {
    if (!c.publicKey) return;
    navigator.clipboard.writeText(c.publicKey);
    this.toastSvc.success(`Public key for "${c.name}" copied to clipboard`);
  }

  publicKeyToShow(): string {
    return this.generatedPublicKey() || this.form().publicKey || '';
  }

  cancel(): void {
    this.showDrawer.set(false);
    this.showPublicKey.set(false);
    this.generatedPublicKey.set('');
    this.sshKeyAlgorithm.set('rsa');
    this.form.set({ ...EMPTY_FORM });
    this.errorMessage.set(null);
  }

  closeDrawer(): void {
    this.cancel();
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
        publicKey: f.publicKey,
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
        publicKey: f.publicKey,
      };
      this.svc.create(create).subscribe({
        next: () => this.done(),
        error: (err) => this.fail(err),
      });
    }
  }

  async remove(c: Credential): Promise<void> {
    if (!this.canManageCredentials()) return;
    const ok = await this.confirmSvc.ask({
      title: 'Delete credential',
      message: `Delete credential "${c.name}"? Connections that use it will lose authentication. This cannot be undone.`,
      confirmLabel: 'Delete',
      tone: 'danger',
    });
    if (!ok) return;
    this.errorMessage.set(null);
    this.svc.remove(c.id).subscribe({
      next: () => {
        this.toastSvc.success(`Credential "${c.name}" deleted.`);
        this.refresh();
      },
      error: (err) => {
        const msg = this.toMessage(err) || 'Delete failed.';
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
      },
    });
  }

  trackById(_: number, c: Credential): string {
    return c.id;
  }

  private done(): void {
    this.busy.set(false);
    this.toastSvc.success('Credential saved.');
    this.cancel();
    this.refresh();
  }

  private fail(err: any): void {
    this.busy.set(false);
    const msg = this.toMessage(err) || 'Save failed.';
    this.errorMessage.set(msg);
    this.toastSvc.error(msg);
  }

  private toMessage(err: any): string | null {
    return err?.error?.message ?? err?.error?.Message ?? err?.message ?? null;
  }
}
