import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { pickOr } from './json-utils';

export interface AppHost {
  id: string;
  name: string;
  address: string;
}

export interface CreateHostPayload {
  name: string;
  address: string;
}

@Injectable({ providedIn: 'root' })
export class HostsService {
  private readonly http = inject(HttpClient);
  private readonly _list = signal<AppHost[]>([]);
  readonly list = this._list.asReadonly();

  reload(): Observable<AppHost[]> {
    return this.http.get<any[]>('/api/hosts').pipe(
      tap((rows) => this._list.set((rows ?? []).map((r) => this.normalize(r)))),
    ) as unknown as Observable<AppHost[]>;
  }

  create(payload: CreateHostPayload): Observable<AppHost> {
    return this.http.post<any>('/api/hosts', payload);
  }

  update(id: string, payload: CreateHostPayload): Observable<void> {
    return this.http.put<void>(`/api/hosts/${id}`, { id, ...payload });
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`/api/hosts/${id}`);
  }

  private normalize(raw: any): AppHost {
    return {
      id: pickOr(raw, '', 'id', 'Id'),
      name: pickOr(raw, '', 'name', 'Name'),
      address: pickOr(raw, '', 'address', 'Address'),
    };
  }
}
