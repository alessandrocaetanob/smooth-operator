import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { Credentials } from './credentials';
import { Credential, CredentialsService } from '../../services/credentials.service';
import { SecretProvider, SecretProvidersService } from '../../services/secret-providers.service';
import { AuthService } from '../../services/auth.service';
import { OnboardingTourService } from '../../services/onboarding-tour.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';

describe('Credentials', () => {
  let component: Credentials;
  let fixture: ComponentFixture<Credentials>;
  let tour: OnboardingTourService;
  let svc: {
    list: ReturnType<typeof signal<Credential[]>>;
    reload: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
    remove: ReturnType<typeof vi.fn>;
    generateSsh: ReturnType<typeof vi.fn>;
  };
  let providersSvc: {
    list: ReturnType<typeof signal<SecretProvider[]>>;
    reload: ReturnType<typeof vi.fn>;
    listSecrets: ReturnType<typeof vi.fn>;
  };
  let auth: {
    canManageCredentials: ReturnType<typeof signal<boolean>>;
    currentUser: ReturnType<typeof signal<{ id: string } | null>>;
  };
  let confirm: { ask: ReturnType<typeof vi.fn> };
  let toast: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    svc = {
      list: signal<Credential[]>([]),
      reload: vi.fn(() => of([] as Credential[])),
      create: vi.fn(() => of({} as unknown as Credential)),
      update: vi.fn(() => of(undefined)),
      remove: vi.fn(() => of(undefined)),
      generateSsh: vi.fn(() => of({ privateKey: 'PRIV', publicKey: 'PUB' })),
    };
    providersSvc = {
      list: signal<SecretProvider[]>([]),
      reload: vi.fn(() => of([] as SecretProvider[])),
      listSecrets: vi.fn(() => of(['secret-a', 'secret-b'])),
    };
    auth = { canManageCredentials: signal(true), currentUser: signal(null) };
    confirm = { ask: vi.fn() };
    toast = { success: vi.fn(), error: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [Credentials],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: CredentialsService, useValue: svc },
        { provide: SecretProvidersService, useValue: providersSvc },
        { provide: AuthService, useValue: auth },
        { provide: ConfirmDialogService, useValue: confirm },
        { provide: ToastService, useValue: toast },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      common: {
        actions: {
          delete: 'Delete',
        },
      },
      pages: {
        credentials: {
          errors: {
            loadFailed: 'Failed to load.',
            generateSshFailed: 'Failed to generate SSH key.',
            nameUsernameRequired: 'Name and username are required.',
            secretRequired: 'Secret is required when creating a credential.',
            selectProviderRequired: 'Please select a secret provider.',
            vaultSecretRequired: 'Secret value is required when pushing to vault.',
            selectSecretRequired: 'Please select an existing secret to link.',
            deleteFailed: 'Delete failed.',
            saveFailed: 'Save failed.',
          },
          toasts: {
            publicKeyCopied: 'Public key copied to clipboard',
            rowPublicKeyCopied: 'Public key for "{{name}}" copied to clipboard',
            deleted: 'Credential "{{name}}" deleted.',
            updated: 'Credential updated.',
            created: 'Credential saved.',
          },
          confirmDelete: {
            title: 'Delete credential',
            message:
              'Delete credential "{{name}}"? Connections that use it will lose authentication. This cannot be undone.',
          },
        },
      },
    });
    translate.use('en');

    fixture = TestBed.createComponent(Credentials);
    component = fixture.componentInstance;
    tour = TestBed.inject(OnboardingTourService);
  });

  afterEach(() => vi.useRealTimers());

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('refresh / ngOnInit', () => {
    it('ngOnInit calls refresh and reloads providers', () => {
      const spy = vi.spyOn(component, 'refresh');
      component.ngOnInit();
      expect(spy).toHaveBeenCalled();
      expect(providersSvc.reload).toHaveBeenCalled();
    });

    it('refresh clears loading and error on success', () => {
      component.refresh();
      expect(component.loading()).toBe(false);
      expect(component.errorMessage()).toBeNull();
    });

    it('refresh sets errorMessage on failure', () => {
      svc.reload.mockReturnValueOnce(throwError(() => ({ error: { Message: 'down' } })));
      component.refresh();
      expect(component.errorMessage()).toBe('down');
    });

    it('refresh falls back to default error', () => {
      svc.reload.mockReturnValueOnce(throwError(() => ({})));
      component.refresh();
      expect(component.errorMessage()).toBe('Failed to load.');
    });
  });

  describe('drawer state', () => {
    it('newCredential opens empty form', () => {
      component.newCredential();
      expect(component.showDrawer()).toBe(true);
      expect(component.form().id).toBeNull();
    });

    it('newCredential no-ops without permission', () => {
      auth.canManageCredentials.set(false);
      component.newCredential();
      expect(component.showDrawer()).toBe(false);
    });

    it('edit loads credential into form', () => {
      const c: Credential = {
        id: 'c1',
        name: 'cred',
        username: 'admin',
        credentialType: 'password',
        storageMode: 'Local',
      } as Credential;
      component.edit(c);
      expect(component.form().id).toBe('c1');
      expect(component.form().name).toBe('cred');
      expect(component.showDrawer()).toBe(true);
    });

    it('edit loads external storage credential and listSecrets', () => {
      const c: Credential = {
        id: 'c2',
        name: 'ext',
        username: 'admin',
        credentialType: 'password',
        storageMode: 'External',
        secretProviderId: 'p1',
        externalSecretName: 'secret-a',
        publicKey: '',
      } as Credential;
      component.edit(c);
      expect(component.form().storageMode).toBe('External');
      expect(providersSvc.listSecrets).toHaveBeenCalledWith('p1');
      expect(component.availableSecrets()).toEqual(['secret-a', 'secret-b']);
    });

    it('edit no-ops without permission', () => {
      auth.canManageCredentials.set(false);
      component.edit({
        id: 'c1',
        name: '',
        username: '',
        credentialType: 'password',
      } as Credential);
      expect(component.showDrawer()).toBe(false);
    });

    it('cancel resets state', () => {
      component.newCredential();
      component.patch('name', 'X');
      component.cancel();
      expect(component.showDrawer()).toBe(false);
      expect(component.form().name).toBe('');
    });

    it('closeDrawer is an alias for cancel', () => {
      component.newCredential();
      component.closeDrawer();
      expect(component.showDrawer()).toBe(false);
    });
  });

  describe('onProviderChange / loadSecretsForProvider', () => {
    it('clears externalSecretName and loads secrets', () => {
      component.form.update((f) => ({ ...f, externalSecretName: 'old' }));
      component.onProviderChange('p1');
      expect(component.form().externalSecretName).toBe('');
      expect(providersSvc.listSecrets).toHaveBeenCalledWith('p1');
      expect(component.availableSecrets()).toEqual(['secret-a', 'secret-b']);
    });

    it('no-ops when provider id is empty', () => {
      component.onProviderChange('');
      expect(providersSvc.listSecrets).not.toHaveBeenCalled();
    });

    it('handles listSecrets failure gracefully', () => {
      providersSvc.listSecrets.mockReturnValueOnce(throwError(() => new Error('x')));
      component.onProviderChange('p1');
      expect(component.availableSecrets()).toEqual([]);
      expect(component.loadingSecrets()).toBe(false);
    });
  });

  describe('generateSshKey', () => {
    it('no-ops without permission', () => {
      auth.canManageCredentials.set(false);
      component.generateSshKey();
      expect(svc.generateSsh).not.toHaveBeenCalled();
    });

    it('populates secret and public key on success', () => {
      component.generateSshKey();
      expect(svc.generateSsh).toHaveBeenCalledWith('rsa');
      expect(component.form().secret).toBe('PRIV');
      expect(component.generatedPublicKey()).toBe('PUB');
      expect(component.showPublicKey()).toBe(true);
      expect(component.generatingSsh()).toBe(false);
    });

    it('surfaces error message on failure', () => {
      svc.generateSsh.mockReturnValueOnce(throwError(() => ({ message: 'no entropy' })));
      component.generateSshKey();
      expect(component.errorMessage()).toBe('no entropy');
      expect(component.generatingSsh()).toBe(false);
    });

    it('falls back to default error', () => {
      svc.generateSsh.mockReturnValueOnce(throwError(() => ({})));
      component.generateSshKey();
      expect(component.errorMessage()).toBe('Failed to generate SSH key.');
    });

    it('uses selected algorithm', () => {
      component.sshKeyAlgorithm.set('ecdsa');
      component.generateSshKey();
      expect(svc.generateSsh).toHaveBeenCalledWith('ecdsa');
    });
  });

  describe('copy helpers', () => {
    let writeText: ReturnType<typeof vi.fn>;
    let originalClipboard: PropertyDescriptor | undefined;
    beforeEach(() => {
      writeText = vi.fn(() => Promise.resolve());
      originalClipboard = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
      Object.defineProperty(navigator, 'clipboard', {
        configurable: true,
        value: { writeText },
      });
    });
    afterEach(() => {
      if (originalClipboard) {
        Object.defineProperty(navigator, 'clipboard', originalClipboard);
      } else {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        delete (navigator as any).clipboard;
      }
    });

    it('copyPublicKey writes generated key and toasts', () => {
      component.generatedPublicKey.set('PUB');
      component.copyPublicKey();
      expect(writeText).toHaveBeenCalledWith('PUB');
      expect(toast.success).toHaveBeenCalled();
    });

    it('copyPublicKey is no-op when no key', () => {
      component.generatedPublicKey.set('');
      component.form.update((f) => ({ ...f, publicKey: '' }));
      component.copyPublicKey();
      expect(writeText).not.toHaveBeenCalled();
    });

    it('copyRowPublicKey writes per-row key and shows feedback for 2s', () => {
      vi.useFakeTimers();
      const c: Credential = {
        id: 'c1',
        name: 'k',
        username: '',
        credentialType: 'private_key',
        publicKey: 'rowkey',
      } as Credential;
      component.copyRowPublicKey(c);
      expect(writeText).toHaveBeenCalledWith('rowkey');
      expect(component.copiedKeyIds().has('c1')).toBe(true);
      vi.advanceTimersByTime(2100);
      expect(component.copiedKeyIds().has('c1')).toBe(false);
    });

    it('copyRowPublicKey is no-op without publicKey', () => {
      const c: Credential = {
        id: 'c1',
        name: 'k',
        username: '',
        credentialType: 'private_key',
      } as Credential;
      component.copyRowPublicKey(c);
      expect(writeText).not.toHaveBeenCalled();
    });

    it('publicKeyToShow prefers generated over form value', () => {
      component.generatedPublicKey.set('GEN');
      component.form.update((f) => ({ ...f, publicKey: 'FORM' }));
      expect(component.publicKeyToShow()).toBe('GEN');
      component.generatedPublicKey.set('');
      expect(component.publicKeyToShow()).toBe('FORM');
      component.form.update((f) => ({ ...f, publicKey: '' }));
      expect(component.publicKeyToShow()).toBe('');
    });
  });

  describe('save() success and failure', () => {
    function fillValid() {
      component.form.set({
        id: null,
        name: 'n',
        username: 'u',
        secret: 's',
        credentialType: 'password',
        publicKey: '',
        storageMode: 'Local',
        secretProviderId: '',
        pushToVault: true,
        externalSecretName: '',
        externalSecretVersion: '',
      });
    }

    it('no-ops without permission', () => {
      auth.canManageCredentials.set(false);
      fillValid();
      component.save();
      expect(svc.create).not.toHaveBeenCalled();
    });

    it('no-ops when busy', () => {
      component.busy.set(true);
      fillValid();
      component.save();
      expect(svc.create).not.toHaveBeenCalled();
    });

    it('creates a new local credential and toasts', () => {
      const completeStep = vi.spyOn(tour, 'completeStep');
      fillValid();
      component.save();
      expect(svc.create).toHaveBeenCalledWith(
        expect.objectContaining({ name: 'n', secret: 's', storageMode: 'Local' }),
      );
      expect(toast.success).toHaveBeenCalledWith('Credential saved.');
      expect(completeStep).toHaveBeenCalledWith('credential');
    });

    it('updates an existing credential', () => {
      const completeStep = vi.spyOn(tour, 'completeStep');
      fillValid();
      component.form.update((f) => ({ ...f, id: 'c1', secret: '' }));
      component.save();
      expect(svc.update).toHaveBeenCalledWith(
        'c1',
        expect.objectContaining({ name: 'n', storageMode: 'Local' }),
      );
      expect(toast.success).toHaveBeenCalledWith('Credential updated.');
      expect(completeStep).not.toHaveBeenCalled();
    });

    it('builds external push-to-vault create payload', () => {
      component.form.set({
        id: null,
        name: 'n',
        username: 'u',
        secret: 's',
        credentialType: 'password',
        publicKey: '',
        storageMode: 'External',
        secretProviderId: 'p1',
        pushToVault: true,
        externalSecretName: '',
        externalSecretVersion: '',
      });
      component.save();
      expect(svc.create).toHaveBeenCalledWith(
        expect.objectContaining({
          storageMode: 'External',
          secretProviderId: 'p1',
          pushToVault: true,
          secret: 's',
        }),
      );
    });

    it('builds external link-mode create payload', () => {
      component.form.set({
        id: null,
        name: 'n',
        username: 'u',
        secret: '',
        credentialType: 'password',
        publicKey: '',
        storageMode: 'External',
        secretProviderId: 'p1',
        pushToVault: false,
        externalSecretName: 'my-secret',
        externalSecretVersion: 'v3',
      });
      component.save();
      expect(svc.create).toHaveBeenCalledWith(
        expect.objectContaining({
          storageMode: 'External',
          externalSecretName: 'my-secret',
          externalSecretVersion: 'v3',
        }),
      );
    });

    it('builds external update with new secret push-to-vault', () => {
      component.form.set({
        id: 'c1',
        name: 'n',
        username: 'u',
        secret: 'new-secret',
        credentialType: 'password',
        publicKey: '',
        storageMode: 'External',
        secretProviderId: 'p1',
        pushToVault: true,
        externalSecretName: '',
        externalSecretVersion: '',
      });
      component.save();
      expect(svc.update).toHaveBeenCalledWith(
        'c1',
        expect.objectContaining({ pushToVault: true, secret: 'new-secret' }),
      );
    });

    it('builds external update with link-mode', () => {
      component.form.set({
        id: 'c1',
        name: 'n',
        username: 'u',
        secret: '',
        credentialType: 'password',
        publicKey: '',
        storageMode: 'External',
        secretProviderId: 'p1',
        pushToVault: false,
        externalSecretName: 'linked',
        externalSecretVersion: '',
      });
      component.save();
      expect(svc.update).toHaveBeenCalledWith(
        'c1',
        expect.objectContaining({
          externalSecretName: 'linked',
          externalSecretVersion: undefined,
        }),
      );
    });

    it('surfaces backend error', () => {
      svc.create.mockReturnValueOnce(throwError(() => ({ message: 'dup' })));
      fillValid();
      component.save();
      expect(component.errorMessage()).toBe('dup');
      expect(toast.error).toHaveBeenCalled();
    });

    it('falls back to default save error', () => {
      svc.create.mockReturnValueOnce(throwError(() => ({})));
      fillValid();
      component.save();
      expect(component.errorMessage()).toBe('Save failed.');
    });
  });

  describe('remove()', () => {
    const sample: Credential = {
      id: 'c1',
      name: 'cred',
      username: 'admin',
      credentialType: 'password',
    } as Credential;

    it('no-ops without permission', async () => {
      auth.canManageCredentials.set(false);
      await component.remove(sample);
      expect(confirm.ask).not.toHaveBeenCalled();
    });

    it('cancels when confirm returns false', async () => {
      confirm.ask.mockResolvedValueOnce(false);
      await component.remove(sample);
      expect(svc.remove).not.toHaveBeenCalled();
    });

    it('removes when confirmed', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      await component.remove(sample);
      expect(svc.remove).toHaveBeenCalledWith('c1');
      expect(toast.success).toHaveBeenCalledWith('Credential "cred" deleted.');
    });

    it('surfaces error', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      svc.remove.mockReturnValueOnce(throwError(() => ({ message: 'in-use' })));
      await component.remove(sample);
      expect(component.errorMessage()).toBe('in-use');
      expect(toast.error).toHaveBeenCalled();
    });

    it('falls back to default error', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      svc.remove.mockReturnValueOnce(throwError(() => ({})));
      await component.remove(sample);
      expect(component.errorMessage()).toBe('Delete failed.');
    });
  });

  describe('search and computed', () => {
    it('filteredCredentials filters by name/username/credentialType', () => {
      svc.list.set([
        { id: 'a', name: 'aws', username: 'admin', credentialType: 'password' } as Credential,
        { id: 'b', name: 'ssh', username: 'root', credentialType: 'private_key' } as Credential,
      ]);
      component.searchQuery.set('aws');
      expect(component.filteredCredentials().map((c) => c.id)).toEqual(['a']);
      component.searchQuery.set('private_key');
      expect(component.filteredCredentials().map((c) => c.id)).toEqual(['b']);
    });

    it('filteredCredentials returns all when no query', () => {
      svc.list.set([{ id: 'a' } as Credential, { id: 'b' } as Credential]);
      expect(component.filteredCredentials().length).toBe(2);
    });

    it('isFiltered toggles based on searchQuery', () => {
      expect(component.isFiltered()).toBe(false);
      component.searchQuery.set('x');
      expect(component.isFiltered()).toBe(true);
    });

    it('isExternal reflects storageMode', () => {
      expect(component.isExternal()).toBe(false);
      component.form.update((f) => ({ ...f, storageMode: 'External' }));
      expect(component.isExternal()).toBe(true);
    });

    it('trackById/Provider return ids', () => {
      expect(component.trackById(0, { id: 'a' } as Credential)).toBe('a');
      expect(component.trackByProvider(0, { id: 'p1' } as SecretProvider)).toBe('p1');
    });
  });

  describe('validateSaveForm via save()', () => {
    it('rejects empty name and stops save', () => {
      component.form.set({
        id: null,
        name: ' ',
        username: 'u',
        secret: 's',
        credentialType: 'password',
        publicKey: '',
        storageMode: 'Local',
        secretProviderId: '',
        pushToVault: true,
        externalSecretName: '',
        externalSecretVersion: '',
      });
      component.save();
      expect(component.errorMessage()).toContain('Name and username');
      expect(svc.create).not.toHaveBeenCalled();
    });

    it('rejects local credential without secret on create', () => {
      component.form.set({
        id: null,
        name: 'n',
        username: 'u',
        secret: '',
        credentialType: 'password',
        publicKey: '',
        storageMode: 'Local',
        secretProviderId: '',
        pushToVault: true,
        externalSecretName: '',
        externalSecretVersion: '',
      });
      component.save();
      expect(component.errorMessage()).toContain('Secret is required');
      expect(svc.create).not.toHaveBeenCalled();
    });

    it('rejects external save without provider', () => {
      component.form.set({
        id: null,
        name: 'n',
        username: 'u',
        secret: '',
        credentialType: 'password',
        publicKey: '',
        storageMode: 'External',
        secretProviderId: '',
        pushToVault: true,
        externalSecretName: '',
        externalSecretVersion: '',
      });
      component.save();
      expect(component.errorMessage()).toContain('secret provider');
    });

    it('rejects external push-to-vault on create with empty secret', () => {
      component.form.set({
        id: null,
        name: 'n',
        username: 'u',
        secret: '',
        credentialType: 'password',
        publicKey: '',
        storageMode: 'External',
        secretProviderId: 'p1',
        pushToVault: true,
        externalSecretName: '',
        externalSecretVersion: '',
      });
      component.save();
      expect(component.errorMessage()).toContain('pushing to vault');
    });

    it('rejects external link-mode without externalSecretName', () => {
      component.form.set({
        id: null,
        name: 'n',
        username: 'u',
        secret: '',
        credentialType: 'password',
        publicKey: '',
        storageMode: 'External',
        secretProviderId: 'p1',
        pushToVault: false,
        externalSecretName: '',
        externalSecretVersion: '',
      });
      component.save();
      expect(component.errorMessage()).toContain('existing secret');
    });
  });
});
