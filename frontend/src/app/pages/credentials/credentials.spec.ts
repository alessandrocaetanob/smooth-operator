import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';

import { Credentials } from './credentials';

describe('Credentials', () => {
  let component: Credentials;
  let fixture: ComponentFixture<Credentials>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Credentials],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Credentials);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('validateSaveForm', () => {
    const setForm = (overrides: Partial<ReturnType<(typeof component)['form']>>) => {
      component.form.update((f) => ({ ...f, ...overrides }));
    };

    it('rejects when name or username is missing', () => {
      setForm({ name: '', username: 'u' });
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect((component as any).validateSaveForm()).toBe(false);
      expect(component.errorMessage()).toContain('Name and username');
    });

    it('rejects local credential without secret on create', () => {
      setForm({ name: 'n', username: 'u', storageMode: 'Local', id: null, secret: '' });
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect((component as any).validateSaveForm()).toBe(false);
      expect(component.errorMessage()).toContain('Secret is required');
    });

    it('accepts local credential with secret on create', () => {
      setForm({ name: 'n', username: 'u', storageMode: 'Local', id: null, secret: 's' });
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect((component as any).validateSaveForm()).toBe(true);
    });

    it('accepts local credential update without secret', () => {
      setForm({ name: 'n', username: 'u', storageMode: 'Local', id: 'abc', secret: '' });
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect((component as any).validateSaveForm()).toBe(true);
    });
  });

  describe('validateExternalStorage', () => {
    const baseExternal = {
      name: 'n',
      username: 'u',
      storageMode: 'External' as const,
      secretProviderId: 'p1',
      pushToVault: true,
      id: null as string | null,
      secret: 's',
      externalSecretName: '',
    };

    it('rejects when provider not selected', () => {
      component.form.update((f) => ({ ...f, ...baseExternal, secretProviderId: '' }));
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect((component as any).validateSaveForm()).toBe(false);
      expect(component.errorMessage()).toContain('secret provider');
    });

    it('rejects push-to-vault on create without secret', () => {
      component.form.update((f) => ({
        ...f,
        ...baseExternal,
        pushToVault: true,
        id: null,
        secret: '',
      }));
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect((component as any).validateSaveForm()).toBe(false);
      expect(component.errorMessage()).toContain('pushing to vault');
    });

    it('rejects link-mode without external secret name', () => {
      component.form.update((f) => ({
        ...f,
        ...baseExternal,
        pushToVault: false,
        externalSecretName: '',
      }));
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect((component as any).validateSaveForm()).toBe(false);
      expect(component.errorMessage()).toContain('existing secret');
    });

    it('accepts valid external push-to-vault create', () => {
      component.form.update((f) => ({ ...f, ...baseExternal }));
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect((component as any).validateSaveForm()).toBe(true);
    });

    it('accepts valid external link-mode', () => {
      component.form.update((f) => ({
        ...f,
        ...baseExternal,
        pushToVault: false,
        externalSecretName: 'my-secret',
      }));
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect((component as any).validateSaveForm()).toBe(true);
    });

    it('accepts push-to-vault update without secret when id exists', () => {
      component.form.update((f) => ({
        ...f,
        ...baseExternal,
        id: 'cred-1',
        pushToVault: true,
        secret: '',
      }));
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect((component as any).validateSaveForm()).toBe(true);
    });

    it('accepts valid external link-mode with explicit version', () => {
      component.form.update((f) => ({
        ...f,
        ...baseExternal,
        pushToVault: false,
        externalSecretName: 'my-secret',
        externalSecretVersion: 'v2',
      }));
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect((component as any).validateSaveForm()).toBe(true);
    });
  });
});
