import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { pick, pickOr, RawRecord } from './json-utils';

export interface Vault {
  id: string;
  name: string;
  parentGroupId?: string | null;
  userCount?: number | null;
  groupCount?: number | null;
  recordingEnabled?: boolean;
  recordingIncludeKeys?: boolean;
  recordingRetentionDays?: number | null;
}

export interface SaveVaultPayload {
  name: string;
  parentGroupId?: string | null;
  recordingEnabled?: boolean;
  recordingIncludeKeys?: boolean;
  recordingRetentionDays?: number | null;
}

export interface VaultAssignments {
  userIds: string[];
  groupIds: string[];
}

@Injectable({ providedIn: 'root' })
export class VaultsService {
  private readonly http = inject(HttpClient);
  private readonly _list = signal<Vault[]>([]);
  readonly list = this._list.asReadonly();

  reload(): Observable<Vault[]> {
    return this.http
      .get<RawRecord[]>('/api/vaults')
      .pipe(
        tap((rows) => this._list.set((rows ?? []).map((r) => this.normalize(r)))),
      ) as unknown as Observable<Vault[]>;
  }

  create(payload: SaveVaultPayload): Observable<Vault> {
    return this.http.post<Vault>('/api/vaults', payload);
  }

  update(id: string, payload: SaveVaultPayload): Observable<void> {
    return this.http.put<void>(`/api/vaults/${id}`, payload);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`/api/vaults/${id}`);
  }

  getAssignments(id: string): Observable<VaultAssignments> {
    return this.http.get<VaultAssignments>(`/api/vaults/${id}/assignments`);
  }

  setAssignments(id: string, payload: VaultAssignments): Observable<void> {
    return this.http.put<void>(`/api/vaults/${id}/assignments`, payload);
  }

  private normalize(raw: RawRecord): Vault {
    return {
      id: pickOr(raw, '', 'id', 'Id'),
      name: pickOr(raw, '', 'name', 'Name'),
      parentGroupId: pick<string>(raw, 'parentGroupId', 'ParentGroupId') ?? null,
      userCount: pick<number>(raw, 'userCount', 'UserCount') ?? null,
      groupCount: pick<number>(raw, 'groupCount', 'GroupCount') ?? null,
      recordingEnabled: pickOr(raw, false, 'recordingEnabled', 'RecordingEnabled'),
      recordingIncludeKeys: pickOr(raw, false, 'recordingIncludeKeys', 'RecordingIncludeKeys'),
      recordingRetentionDays:
        pick<number>(raw, 'recordingRetentionDays', 'RecordingRetentionDays') ?? null,
    };
  }
}
