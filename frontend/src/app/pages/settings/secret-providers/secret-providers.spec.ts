import { TestBed } from '@angular/core/testing';
import type { WritableSignal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { of, throwError } from 'rxjs';

import { SecretProviders } from './secret-providers';
import { SecretProvidersService, SecretProvider } from '../../../services/secret-providers.service';

const mockProvider: SecretProvider = {
  id: 'sp1',
  name: 'My KV',
  type: 'AzureKeyVault',
  isEnabled: true,
  hasClientSecret: true,
  vaultUri: 'https://myvault.vault.azure.net/',
  tenantId: 'tenant-1',
  clientId: 'client-1',
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: '2024-01-02T00:00:00Z',
};

describe('SecretProviders', () => {
  let fixture: ReturnType<typeof TestBed.createComponent<SecretProviders>>;
  let component: SecretProviders;
  let svc: SecretProvidersService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SecretProviders],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    svc = TestBed.inject(SecretProvidersService);
    vi.spyOn(svc, 'reload').mockReturnValue(of([]));

    fixture = TestBed.createComponent(SecretProviders);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should call reload on init', () => {
    fixture.detectChanges();
    expect(svc.reload).toHaveBeenCalledOnce();
  });

  it('should show form when newProvider() is called', () => {
    fixture.detectChanges();
    expect(component.showForm()).toBe(false);
    component.newProvider();
    expect(component.showForm()).toBe(true);
    expect(component.isEditing()).toBe(false);
  });

  it('should populate form and show edit mode when editProvider() is called', () => {
    fixture.detectChanges();
    component.editProvider(mockProvider);
    expect(component.showForm()).toBe(true);
    expect(component.isEditing()).toBe(true);
    const f = component.form();
    expect(f.id).toBe('sp1');
    expect(f.name).toBe('My KV');
    expect(f.vaultUri).toBe('https://myvault.vault.azure.net/');
    expect(f.tenantId).toBe('tenant-1');
    expect(f.clientId).toBe('client-1');
    expect(f.clientSecret).toBe('');
  });

  it('should reset form and hide form on cancel()', () => {
    fixture.detectChanges();
    component.editProvider(mockProvider);
    component.cancel();
    expect(component.showForm()).toBe(false);
    expect(component.form().id).toBeNull();
    expect(component.isEditing()).toBe(false);
  });

  it('should set errorMessage when required fields are missing on save()', () => {
    fixture.detectChanges();
    component.newProvider();
    component.save();
    expect(component.errorMessage()).toBeTruthy();
  });

  it('should call svc.create() with payload on save for new provider', () => {
    vi.spyOn(svc, 'create').mockReturnValue(of(mockProvider));
    vi.spyOn(svc, 'reload').mockReturnValue(of([]));

    fixture.detectChanges();
    component.newProvider();
    component.patch('name', 'My KV');
    component.patch('vaultUri', 'https://myvault.vault.azure.net/');
    component.patch('tenantId', 'tenant-1');
    component.patch('clientId', 'client-1');
    component.patch('clientSecret', 'secret');
    component.save();

    expect(svc.create).toHaveBeenCalledWith({
      name: 'My KV',
      vaultUri: 'https://myvault.vault.azure.net/',
      tenantId: 'tenant-1',
      clientId: 'client-1',
      clientSecret: 'secret',
    });
  });

  it('should call svc.update() with payload on save for existing provider', () => {
    vi.spyOn(svc, 'update').mockReturnValue(of(mockProvider));
    vi.spyOn(svc, 'reload').mockReturnValue(of([]));

    fixture.detectChanges();
    component.editProvider(mockProvider);
    component.patch('name', 'Renamed');
    component.save();

    expect(svc.update).toHaveBeenCalledWith('sp1', expect.objectContaining({ name: 'Renamed' }));
  });

  it('should call svc.test() and set testBusy on testConnection()', () => {
    vi.spyOn(svc, 'test').mockReturnValue(of({ success: true }));

    fixture.detectChanges();
    component.testConnection(mockProvider);

    expect(svc.test).toHaveBeenCalledWith('sp1');
    expect(component.testBusy()).toBeNull();
  });

  it('renders per-provider action buttons labelled with the provider name', () => {
    fixture.detectChanges();

    // Populate the service-backed list so the @for provider rows render.
    const listSignal = (svc as unknown as { _list: WritableSignal<SecretProvider[]> })._list;
    listSignal.set([mockProvider]);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const testBtn = el.querySelector('button[aria-label="Test connection for My KV"]');
    const editBtn = el.querySelector('button[aria-label="Edit provider My KV"]');
    const deleteBtn = el.querySelector('button[aria-label="Delete provider My KV"]');

    expect(testBtn).not.toBeNull();
    expect(editBtn).not.toBeNull();
    expect(deleteBtn).not.toBeNull();
  });

  it('should set errorMessage when create fails', () => {
    vi.spyOn(svc, 'create').mockReturnValue(throwError(() => ({ message: 'Server error' })));

    fixture.detectChanges();
    component.newProvider();
    component.patch('name', 'Fail KV');
    component.patch('vaultUri', 'https://fail.vault.azure.net/');
    component.patch('tenantId', 'tenant-fail');
    component.patch('clientId', 'client-fail');
    component.patch('clientSecret', 'secret');
    component.save();

    expect(component.errorMessage()).toBeTruthy();
    expect(component.busy()).toBe(false);
  });
});
