import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  SecretProvider,
  SecretProvidersService,
  CreateSecretProviderPayload,
  UpdateSecretProviderPayload,
} from '../../../services/secret-providers.service';
import { ConfirmDialogService } from '../../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../../shared/toast/toast.service';

interface FormState {
  id: string | null;
  name: string;
  vaultUri: string;
  tenantId: string;
  clientId: string;
  clientSecret: string;
  isEnabled: boolean;
}

const EMPTY_FORM: FormState = {
  id: null,
  name: '',
  vaultUri: '',
  tenantId: '',
  clientId: '',
  clientSecret: '',
  isEnabled: true,
};

@Component({
  selector: 'app-secret-providers',
  imports: [FormsModule],
  templateUrl: './secret-providers.html',
})
export class SecretProviders implements OnInit {
  private readonly svc = inject(SecretProvidersService);
  private readonly confirmSvc = inject(ConfirmDialogService);
  private readonly toastSvc = inject(ToastService);

  readonly providers = this.svc.list;
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly testBusy = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly showForm = signal(false);
  readonly form = signal<FormState>({ ...EMPTY_FORM });

  readonly isEditing = computed(() => !!this.form().id);

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
        this.errorMessage.set(this.toMessage(err) || 'Failed to load secret providers.');
      },
    });
  }

  newProvider(): void {
    this.form.set({ ...EMPTY_FORM });
    this.errorMessage.set(null);
    this.showForm.set(true);
  }

  editProvider(p: SecretProvider): void {
    this.form.set({
      id: p.id,
      name: p.name,
      vaultUri: p.vaultUri ?? '',
      tenantId: p.tenantId ?? '',
      clientId: p.clientId ?? '',
      clientSecret: '',
      isEnabled: p.isEnabled,
    });
    this.errorMessage.set(null);
    this.showForm.set(true);
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.set({ ...EMPTY_FORM });
    this.errorMessage.set(null);
  }

  patch<K extends keyof FormState>(key: K, value: FormState[K]): void {
    this.form.update((f) => ({ ...f, [key]: value }));
  }

  save(): void {
    if (this.busy()) return;
    const f = this.form();
    if (!f.name.trim() || !f.vaultUri.trim() || !f.tenantId.trim() || !f.clientId.trim()) {
      this.errorMessage.set('Name, Vault URI, Tenant ID, and Client ID are required.');
      return;
    }
    if (!f.id && !f.clientSecret.trim()) {
      this.errorMessage.set('Client Secret is required when creating a provider.');
      return;
    }
    this.busy.set(true);
    this.errorMessage.set(null);
    if (f.id) {
      const payload: UpdateSecretProviderPayload = {
        name: f.name.trim(),
        vaultUri: f.vaultUri.trim(),
        tenantId: f.tenantId.trim(),
        clientId: f.clientId.trim(),
        isEnabled: f.isEnabled,
      };
      if (f.clientSecret.trim()) payload.clientSecret = f.clientSecret.trim();
      this.svc.update(f.id, payload).subscribe({
        next: () => {
          this.busy.set(false);
          this.toastSvc.success('Secret provider updated.');
          this.cancel();
          this.refresh();
        },
        error: (err) => {
          this.busy.set(false);
          this.errorMessage.set(this.toMessage(err) || 'Failed to save provider.');
        },
      });
    } else {
      const payload: CreateSecretProviderPayload = {
        name: f.name.trim(),
        vaultUri: f.vaultUri.trim(),
        tenantId: f.tenantId.trim(),
        clientId: f.clientId.trim(),
        clientSecret: f.clientSecret.trim(),
      };
      this.svc.create(payload).subscribe({
        next: () => {
          this.busy.set(false);
          this.toastSvc.success('Secret provider created.');
          this.cancel();
          this.refresh();
        },
        error: (err) => {
          this.busy.set(false);
          this.errorMessage.set(this.toMessage(err) || 'Failed to create provider.');
        },
      });
    }
  }

  testConnection(p: SecretProvider): void {
    if (this.testBusy()) return;
    this.testBusy.set(p.id);
    this.svc.test(p.id).subscribe({
      next: (r) => {
        this.testBusy.set(null);
        if (r.success) {
          this.toastSvc.success(`"${p.name}" connection successful.`);
        } else {
          this.toastSvc.error(`"${p.name}" connection failed.`);
        }
      },
      error: (err) => {
        this.testBusy.set(null);
        this.toastSvc.error(this.toMessage(err) || `"${p.name}" connection failed.`);
      },
    });
  }

  async remove(p: SecretProvider): Promise<void> {
    const ok = await this.confirmSvc.ask({
      title: 'Delete secret provider',
      message: `Delete "${p.name}"? Credentials linked to this provider will stop working. This cannot be undone.`,
      confirmLabel: 'Delete',
      tone: 'danger',
    });
    if (!ok) return;
    this.svc.remove(p.id).subscribe({
      next: () => {
        this.toastSvc.success(`"${p.name}" deleted.`);
        this.refresh();
      },
      error: (err) => {
        this.toastSvc.error(this.toMessage(err) || 'Delete failed.');
      },
    });
  }

  trackById(_: number, p: SecretProvider): string {
    return p.id;
  }

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
