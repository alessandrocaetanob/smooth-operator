import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap, map } from 'rxjs';
import { pickOr } from './json-utils';

export interface AppUser {
  id: string;
  email: string;
  name: string;
  isActive: boolean;
  linkedToEntra: boolean;
  hasPassword: boolean;
  createdAt: string;
  roles: string[];
  vaultIds: string[];
}

export interface AppRole {
  name: string;
  description: string;
}

export interface InviteResult {
  message: string;
  inviteUrl: string;
  emailSent: boolean;
  emailError?: string | null;
}

export interface EffectiveVaultRef {
  id: string;
  name: string;
  parentGroupId?: string | null;
}

export interface EffectiveGroupVaults {
  groupId: string;
  groupName: string;
  vaults: EffectiveVaultRef[];
}

export interface EffectiveVaults {
  userId: string;
  direct: EffectiveVaultRef[];
  viaGroups: EffectiveGroupVaults[];
  merged: EffectiveVaultRef[];
}

function normalizeVault(raw: any): EffectiveVaultRef {
  return {
    id: pickOr(raw, '', 'id', 'Id'),
    name: pickOr(raw, '', 'name', 'Name'),
    parentGroupId: raw?.parentGroupId ?? raw?.ParentGroupId ?? null,
  };
}

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);
  private readonly _list = signal<AppUser[]>([]);
  readonly list = this._list.asReadonly();

  reload(): Observable<AppUser[]> {
    return this.http.get<any[]>('/api/users').pipe(
      tap((rows) => this._list.set((rows ?? []).map((r) => this.normalize(r)))),
    ) as unknown as Observable<AppUser[]>;
  }

  setActive(id: string, isActive: boolean): Observable<void> {
    return this.http.patch<void>(`/api/users/${id}/active`, { isActive });
  }

  rename(id: string, name: string): Observable<void> {
    return this.http.put<void>(`/api/users/${id}`, { name });
  }

  roleCatalog(): Observable<AppRole[]> {
    return this.http.get<any[]>('/api/users/roles').pipe(
      map((rows) =>
        (rows ?? []).map((raw) => ({
          name: pickOr(raw, '', 'name', 'Name'),
          description: pickOr(raw, '', 'description', 'Description'),
        })),
      ),
    );
  }

  setRole(id: string, role: string): Observable<void> {
    return this.http.put<void>(`/api/users/${id}/role`, { role });
  }

  setVaultAssignments(id: string, vaultIds: string[]): Observable<void> {
    return this.http.put<void>(`/api/users/${id}/vaults`, { vaultIds });
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`/api/users/${id}`);
  }

  getEffectiveVaults(id: string): Observable<EffectiveVaults> {
    return this.http.get<any>(`/api/users/${id}/effective-vaults`).pipe(
      map((raw) => ({
        userId: pickOr(raw, '', 'userId', 'UserId'),
        direct: (pickOr(raw, [] as any[], 'direct', 'Direct') ?? []).map(normalizeVault),
        viaGroups: (pickOr(raw, [] as any[], 'viaGroups', 'ViaGroups') ?? []).map((g: any) => ({
          groupId: pickOr(g, '', 'groupId', 'GroupId'),
          groupName: pickOr(g, '', 'groupName', 'GroupName'),
          vaults: (pickOr(g, [] as any[], 'vaults', 'Vaults') ?? []).map(normalizeVault),
        })),
        merged: (pickOr(raw, [] as any[], 'merged', 'Merged') ?? []).map(normalizeVault),
      })),
    );
  }

  invite(payload: { email: string; name?: string; password?: string; role?: string }): Observable<InviteResult> {
    return this.http.post<any>('/api/auth/invite', payload).pipe(
      map((raw) => ({
        message: pickOr(raw, '', 'message', 'Message'),
        inviteUrl: pickOr(raw, '', 'inviteUrl', 'InviteUrl'),
        emailSent: pickOr(raw, false, 'emailSent', 'EmailSent'),
        emailError: pickOr(raw, null as string | null, 'emailError', 'EmailError'),
      })),
    );
  }

  private normalize(raw: any): AppUser {
    return {
      id: pickOr(raw, '', 'id', 'Id'),
      email: pickOr(raw, '', 'email', 'Email'),
      name: pickOr(raw, '', 'name', 'Name'),
      isActive: pickOr(raw, true, 'isActive', 'IsActive'),
      linkedToEntra: pickOr(raw, false, 'linkedToEntra', 'LinkedToEntra'),
      hasPassword: pickOr(raw, false, 'hasPassword', 'HasPassword'),
      createdAt: pickOr(raw, '', 'createdAt', 'CreatedAt'),
      roles: pickOr(raw, [] as string[], 'roles', 'Roles'),
      vaultIds: pickOr(raw, [] as string[], 'vaultIds', 'VaultIds'),
    };
  }
}
