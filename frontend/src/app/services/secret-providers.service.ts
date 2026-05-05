import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { pickOr, RawRecord } from './json-utils';

export interface SecretProvider {
  id: string;
  name: string;
  type: string;
  isEnabled: boolean;
  hasClientSecret: boolean;
  vaultUri?: string;
  tenantId?: string;
  clientId?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSecretProviderPayload {
  name: string;
  vaultUri: string;
  tenantId: string;
  clientId: string;
  clientSecret: string;
}

export interface UpdateSecretProviderPayload {
  name?: string;
  vaultUri?: string;
  tenantId?: string;
  clientId?: string;
  clientSecret?: string | null;
  isEnabled?: boolean;
}

@Injectable({ providedIn: 'root' })
export class SecretProvidersService {
  private readonly http = inject(HttpClient);
  private readonly _list = signal<SecretProvider[]>([]);
  readonly list = this._list.asReadonly();

  reload(): Observable<SecretProvider[]> {
    return this.http
      .get<RawRecord[]>('/api/secretproviders')
      .pipe(
        tap((rows) => this._list.set((rows ?? []).map((r) => this.normalize(r)))),
      ) as unknown as Observable<SecretProvider[]>;
  }

  create(payload: CreateSecretProviderPayload): Observable<SecretProvider> {
    return this.http.post<SecretProvider>('/api/secretproviders', payload);
  }

  update(id: string, payload: UpdateSecretProviderPayload): Observable<SecretProvider> {
    return this.http.put<SecretProvider>(`/api/secretproviders/${id}`, payload);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`/api/secretproviders/${id}`);
  }

  test(id: string): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`/api/secretproviders/${id}/test`, {});
  }

  listSecrets(providerId: string): Observable<string[]> {
    return this.http.get<string[]>(`/api/secretproviders/${providerId}/secrets`);
  }

  private normalize(raw: RawRecord): SecretProvider {
    return {
      id: pickOr(raw, '', 'id', 'Id'),
      name: pickOr(raw, '', 'name', 'Name'),
      type: pickOr(raw, 'AzureKeyVault', 'type', 'Type'),
      isEnabled: pickOr(raw, true, 'isEnabled', 'IsEnabled'),
      hasClientSecret: pickOr(raw, false, 'hasClientSecret', 'HasClientSecret'),
      vaultUri: pickOr(raw, '', 'vaultUri', 'VaultUri'),
      tenantId: pickOr(raw, '', 'tenantId', 'TenantId'),
      clientId: pickOr(raw, '', 'clientId', 'ClientId'),
      createdAt: pickOr(raw, '', 'createdAt', 'CreatedAt'),
      updatedAt: pickOr(raw, '', 'updatedAt', 'UpdatedAt'),
    };
  }
}
