import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { pickOr, RawRecord } from './json-utils';

export type SecretStorageMode = 'Local' | 'External';

export interface Credential {
  id: string;
  name: string;
  username: string;
  credentialType: string;
  publicKey?: string;
  storageMode: SecretStorageMode;
  secretProviderId?: string;
  secretProviderName?: string;
  externalSecretName?: string;
  externalSecretVersion?: string;
}

export interface CreateCredentialPayload {
  name: string;
  username: string;
  credentialType: string;
  publicKey?: string;
  storageMode?: SecretStorageMode;
  // Local storage
  secret?: string;
  // External / Push-to-vault
  secretProviderId?: string;
  pushToVault?: boolean;
  // External / Link-existing
  externalSecretName?: string;
  externalSecretVersion?: string;
}

export interface UpdateCredentialPayload {
  name: string;
  username: string;
  credentialType: string;
  publicKey?: string;
  storageMode?: SecretStorageMode;
  // Local storage (omit to keep existing)
  secret?: string;
  // External
  secretProviderId?: string;
  pushToVault?: boolean;
  externalSecretName?: string;
  externalSecretVersion?: string;
}

@Injectable({ providedIn: 'root' })
export class CredentialsService {
  private readonly http = inject(HttpClient);
  private readonly _list = signal<Credential[]>([]);
  readonly list = this._list.asReadonly();

  reload(): Observable<Credential[]> {
    return this.http
      .get<RawRecord[]>('/api/credentials')
      .pipe(
        tap((rows) => this._list.set((rows ?? []).map((r) => this.normalize(r)))),
      ) as unknown as Observable<Credential[]>;
  }

  create(payload: CreateCredentialPayload): Observable<Credential> {
    return this.http.post<Credential>('/api/credentials', payload);
  }

  update(id: string, payload: UpdateCredentialPayload): Observable<void> {
    return this.http.put<void>(`/api/credentials/${id}`, { id, ...payload });
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`/api/credentials/${id}`);
  }

  generateSsh(keyType: string): Observable<{ privateKey: string; publicKey: string }> {
    return this.http.post<{ privateKey: string; publicKey: string }>(
      '/api/credentials/generate-ssh',
      { keyType },
    );
  }

  private normalize(raw: RawRecord): Credential {
    return {
      id: pickOr(raw, '', 'id', 'Id'),
      name: pickOr(raw, '', 'name', 'Name'),
      username: pickOr(raw, '', 'username', 'Username'),
      credentialType: pickOr(raw, 'password', 'credentialType', 'CredentialType'),
      publicKey: pickOr(raw, '', 'publicKey', 'PublicKey'),
      storageMode: pickOr(raw, 'Local', 'storageMode', 'StorageMode'),
      secretProviderId: pickOr(raw, undefined, 'secretProviderId', 'SecretProviderId'),
      secretProviderName: pickOr(raw, undefined, 'secretProviderName', 'SecretProviderName'),
      externalSecretName: pickOr(raw, undefined, 'externalSecretName', 'ExternalSecretName'),
      externalSecretVersion: pickOr(
        raw,
        undefined,
        'externalSecretVersion',
        'ExternalSecretVersion',
      ),
    };
  }
}
