import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { pickOr } from './json-utils';

export interface Credential {
  id: string;
  name: string;
  username: string;
  credentialType: 'password' | 'private_key' | 'api_token' | string;
  publicKey?: string;
}

export interface CreateCredentialPayload {
  name: string;
  username: string;
  secret: string;
  credentialType: string;
  publicKey?: string;
}

export interface UpdateCredentialPayload {
  name: string;
  username: string;
  secret?: string; // optional - omit to keep existing secret
  credentialType: string;
  publicKey?: string;
}

@Injectable({ providedIn: 'root' })
export class CredentialsService {
  private readonly http = inject(HttpClient);
  private readonly _list = signal<Credential[]>([]);
  readonly list = this._list.asReadonly();

  reload(): Observable<Credential[]> {
    return this.http
      .get<any[]>('/api/credentials')
      .pipe(
        tap((rows) => this._list.set((rows ?? []).map((r) => this.normalize(r)))),
      ) as unknown as Observable<Credential[]>;
  }

  create(payload: CreateCredentialPayload): Observable<Credential> {
    return this.http.post<any>('/api/credentials', payload);
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

  private normalize(raw: any): Credential {
    return {
      id: pickOr(raw, '', 'id', 'Id'),
      name: pickOr(raw, '', 'name', 'Name'),
      username: pickOr(raw, '', 'username', 'Username'),
      credentialType: pickOr(raw, 'password', 'credentialType', 'CredentialType'),
      publicKey: pickOr(raw, '', 'publicKey', 'PublicKey'),
    };
  }
}
