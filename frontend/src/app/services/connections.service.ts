import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { pick, pickOr } from './json-utils';

export interface ConnectionHostRef {
  id: string;
  name: string;
  address: string;
}

export interface Connection {
  id: string;
  name: string;
  protocol: 'rdp' | 'ssh' | 'vnc' | string;
  hostId: string;
  credentialId?: string | null;
  connectionGroupId?: string | null;
  settings: string;
  host?: ConnectionHostRef | null;
}

export interface CreateConnectionPayload {
  name: string;
  protocol: string;
  hostId: string;
  credentialId?: string | null;
  connectionGroupId?: string | null;
  settings?: string;
}

@Injectable({ providedIn: 'root' })
export class ConnectionsService {
  private readonly http = inject(HttpClient);
  private readonly _list = signal<Connection[]>([]);
  private readonly _loading = signal(false);

  readonly list = this._list.asReadonly();
  readonly listAsMap = computed(() => new Map(this._list().map(c => [c.id, c])));
  readonly loading = this._loading.asReadonly();

  reload(): Observable<Connection[]> {
    this._loading.set(true);
    return this.http.get<any[]>('/api/connections').pipe(
      tap({
        next: (rows) => {
          this._list.set((rows ?? []).map((r) => this.normalize(r)));
          this._loading.set(false);
        },
        error: () => this._loading.set(false),
      }),
    ) as unknown as Observable<Connection[]>;
  }

  get(id: string): Observable<Connection> {
    return this.http
      .get<any>(`/api/connections/${id}`)
      .pipe(tap()) as unknown as Observable<Connection>;
  }

  create(payload: CreateConnectionPayload): Observable<Connection> {
    return this.http.post<any>('/api/connections', payload);
  }

  update(id: string, payload: CreateConnectionPayload): Observable<void> {
    return this.http.put<void>(`/api/connections/${id}`, { id, ...payload });
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`/api/connections/${id}`);
  }

  private normalize(raw: any): Connection {
    const host = pick<any>(raw, 'host', 'Host');
    return {
      id: pickOr(raw, '', 'id', 'Id'),
      name: pickOr(raw, '', 'name', 'Name'),
      protocol: pickOr(raw, 'rdp', 'protocol', 'Protocol'),
      hostId: pickOr(raw, '', 'hostId', 'HostId'),
      credentialId: pick<string>(raw, 'credentialId', 'CredentialId') ?? null,
      connectionGroupId: pick<string>(raw, 'connectionGroupId', 'ConnectionGroupId') ?? null,
      settings: pickOr(raw, '{}', 'settings', 'Settings'),
      host: host
        ? {
            id: pickOr(host, '', 'id', 'Id'),
            name: pickOr(host, '', 'name', 'Name'),
            address: pickOr(host, '', 'address', 'Address'),
          }
        : null,
    };
  }
}
