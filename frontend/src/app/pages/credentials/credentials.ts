import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
  computed,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  Credential,
  CredentialsService,
  CreateCredentialPayload,
  UpdateCredentialPayload,
  SecretStorageMode,
} from '../../services/credentials.service';
import { SecretProvider, SecretProvidersService } from '../../services/secret-providers.service';
import { AuthService } from '../../services/auth.service';
import { OnboardingTourService } from '../../services/onboarding-tour.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';
import { Drawer } from '../../shared/drawer/drawer';
import { EmptyStateCard } from '../../shared/empty-state-card/empty-state-card';

interface FormState {
  id: string | null;
  name: string;
  username: string;
  secret: string;
  credentialType: string;
  publicKey?: string;
  storageMode: SecretStorageMode;
  secretProviderId: string;
  pushToVault: boolean;
  externalSecretName: string;
  externalSecretVersion: string;
}

const EMPTY_FORM: FormState = {
  id: null,
  name: '',
  username: '',
  secret: '',
  credentialType: 'password',
  publicKey: '',
  storageMode: 'Local',
  secretProviderId: '',
  pushToVault: true,
  externalSecretName: '',
  externalSecretVersion: '',
};

@Component({
  selector: 'app-credentials',
  imports: [FormsModule, Drawer, TranslatePipe, EmptyStateCard],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './credentials.html',
  styleUrl: './credentials.css',
})
export class Credentials implements OnInit {
  private readonly svc = inject(CredentialsService);
  private readonly providersSvc = inject(SecretProvidersService);
  private readonly auth = inject(AuthService);
  private readonly tour = inject(OnboardingTourService);
  private readonly confirmSvc = inject(ConfirmDialogService);
  private readonly toastSvc = inject(ToastService);
  private readonly translate = inject(TranslateService);

  readonly credentials = this.svc.list;
  readonly providers = this.providersSvc.list;
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
  readonly loadingSecrets = signal(false);
  readonly availableSecrets = signal<string[]>([]);
  readonly copiedKeyIds = signal(new Set<string>());

  private readonly searchableCredentials = computed(() => {
    return this.credentials().map((c) => ({
      item: c,
      _searchIndex: [c.name ?? '', c.username ?? '', c.credentialType ?? '']
        .join(' ')
        .toLowerCase(),
    }));
  });

  readonly filteredCredentials = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    if (!q) return this.credentials();
    return this.searchableCredentials()
      .filter((c) => c._searchIndex.includes(q))
      .map(({ item }) => item);
  });

  readonly isFiltered = computed(() => this.searchQuery().trim().length > 0);
  readonly isExternal = computed(() => this.form().storageMode === 'External');

  readonly types = [
    { value: 'password', labelKey: 'pages.credentials.form.typePassword' },
    { value: 'private_key', labelKey: 'pages.credentials.form.typePrivateKey' },
  ];

  ngOnInit(): void {
    this.refresh();
    this.providersSvc.reload().subscribe();
  }

  refresh(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.svc.reload().subscribe({
      next: () => this.loading.set(false),
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(
          this.toMessage(err) || this.translate.instant('pages.credentials.errors.loadFailed'),
        );
      },
    });
  }

  newCredential(): void {
    if (!this.canManageCredentials()) return;
    this.form.set({ ...EMPTY_FORM });
    this.availableSecrets.set([]);
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
      storageMode: c.storageMode ?? 'Local',
      secretProviderId: c.secretProviderId ?? '',
      pushToVault: false,
      externalSecretName: c.externalSecretName ?? '',
      externalSecretVersion: c.externalSecretVersion ?? '',
    });
    this.availableSecrets.set([]);
    if (c.storageMode === 'External' && c.secretProviderId) {
      this.loadSecretsForProvider(c.secretProviderId);
    }
    this.errorMessage.set(null);
    this.showDrawer.set(true);
  }

  onProviderChange(providerId: string): void {
    this.patch('secretProviderId', providerId);
    this.patch('externalSecretName', '');
    this.availableSecrets.set([]);
    if (providerId) {
      this.loadSecretsForProvider(providerId);
    }
  }

  private loadSecretsForProvider(providerId: string): void {
    this.loadingSecrets.set(true);
    this.providersSvc.listSecrets(providerId).subscribe({
      next: (secrets) => {
        this.loadingSecrets.set(false);
        this.availableSecrets.set(secrets);
      },
      error: () => {
        this.loadingSecrets.set(false);
        this.availableSecrets.set([]);
      },
    });
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
        this.errorMessage.set(
          this.toMessage(err) ||
            this.translate.instant('pages.credentials.errors.generateSshFailed'),
        );
      },
    });
  }

  copyPublicKey(): void {
    const key = this.publicKeyToShow();
    if (!key) return;
    navigator.clipboard.writeText(key);
    this.toastSvc.success(this.translate.instant('pages.credentials.toasts.publicKeyCopied'));
  }

  copyRowPublicKey(c: Credential): void {
    if (!c.publicKey) return;
    navigator.clipboard.writeText(c.publicKey);
    this.copiedKeyIds.update((s) => new Set([...s, c.id]));
    setTimeout(() => {
      this.copiedKeyIds.update((s) => {
        const next = new Set(s);
        next.delete(c.id);
        return next;
      });
    }, 2000);
    this.toastSvc.success(
      this.translate.instant('pages.credentials.toasts.rowPublicKeyCopied', { name: c.name }),
    );
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
    this.availableSecrets.set([]);
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
    if (!this.validateSaveForm()) return;
    const f = this.form();
    this.busy.set(true);
    this.errorMessage.set(null);
    if (f.id) {
      this.svc.update(f.id, this.buildUpdatePayload(f)).subscribe({
        next: () => this.done(),
        error: (err) => this.fail(err),
      });
    } else {
      this.svc.create(this.buildCreatePayload(f)).subscribe({
        next: () => this.done(),
        error: (err) => this.fail(err),
      });
    }
  }

  private validateSaveForm(): boolean {
    const f = this.form();
    if (!f.name.trim() || !f.username.trim()) {
      this.errorMessage.set(
        this.translate.instant('pages.credentials.errors.nameUsernameRequired'),
      );
      return false;
    }
    if (f.storageMode === 'External') {
      return this.validateExternalStorage(f);
    }
    if (!f.id && !f.secret) {
      this.errorMessage.set(this.translate.instant('pages.credentials.errors.secretRequired'));
      return false;
    }
    return true;
  }

  private validateExternalStorage(f: FormState): boolean {
    if (!f.secretProviderId) {
      this.errorMessage.set(
        this.translate.instant('pages.credentials.errors.selectProviderRequired'),
      );
      return false;
    }
    if (f.pushToVault && !f.id && !f.secret) {
      this.errorMessage.set(this.translate.instant('pages.credentials.errors.vaultSecretRequired'));
      return false;
    }
    if (!f.pushToVault && !f.externalSecretName) {
      this.errorMessage.set(
        this.translate.instant('pages.credentials.errors.selectSecretRequired'),
      );
      return false;
    }
    return true;
  }

  private buildUpdatePayload(f: FormState): UpdateCredentialPayload {
    const upd: UpdateCredentialPayload = {
      name: f.name.trim(),
      username: f.username.trim(),
      credentialType: f.credentialType,
      publicKey: f.publicKey,
      storageMode: f.storageMode,
    };
    if (f.storageMode === 'External') {
      upd.secretProviderId = f.secretProviderId;
      if (f.pushToVault && f.secret) {
        upd.pushToVault = true;
        upd.secret = f.secret;
      } else if (!f.pushToVault) {
        upd.externalSecretName = f.externalSecretName;
        upd.externalSecretVersion = f.externalSecretVersion || undefined;
      }
    } else if (f.secret) {
      upd.secret = f.secret;
    }
    return upd;
  }

  private buildCreatePayload(f: FormState): CreateCredentialPayload {
    const create: CreateCredentialPayload = {
      name: f.name.trim(),
      username: f.username.trim(),
      credentialType: f.credentialType,
      publicKey: f.publicKey,
      storageMode: f.storageMode,
    };
    if (f.storageMode === 'External') {
      create.secretProviderId = f.secretProviderId;
      if (f.pushToVault) {
        create.pushToVault = true;
        create.secret = f.secret;
      } else {
        create.externalSecretName = f.externalSecretName;
        create.externalSecretVersion = f.externalSecretVersion || undefined;
      }
    } else {
      create.secret = f.secret;
    }
    return create;
  }

  async remove(c: Credential): Promise<void> {
    if (!this.canManageCredentials()) return;
    const ok = await this.confirmSvc.ask({
      title: this.translate.instant('pages.credentials.confirmDelete.title'),
      message: this.translate.instant('pages.credentials.confirmDelete.message', {
        name: c.name,
      }),
      confirmLabel: this.translate.instant('common.actions.delete'),
      tone: 'danger',
    });
    if (!ok) return;
    this.errorMessage.set(null);
    this.svc.remove(c.id).subscribe({
      next: () => {
        this.toastSvc.success(
          this.translate.instant('pages.credentials.toasts.deleted', { name: c.name }),
        );
        this.refresh();
      },
      error: (err) => {
        const msg =
          this.toMessage(err) || this.translate.instant('pages.credentials.errors.deleteFailed');
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
      },
    });
  }

  trackById(_: number, c: Credential): string {
    return c.id;
  }

  trackByProvider(_: number, p: SecretProvider): string {
    return p.id;
  }

  private done(): void {
    const isUpdate = !!this.form().id;
    this.busy.set(false);
    this.toastSvc.success(
      this.translate.instant(
        isUpdate ? 'pages.credentials.toasts.updated' : 'pages.credentials.toasts.created',
      ),
    );
    if (!isUpdate) this.tour.completeStep('credential');
    this.cancel();
    this.refresh();
  }

  private fail(err: unknown): void {
    this.busy.set(false);
    const msg =
      this.toMessage(err) || this.translate.instant('pages.credentials.errors.saveFailed');
    this.errorMessage.set(msg);
    this.toastSvc.error(msg);
  }

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
