import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { pickOr, RawRecord } from './json-utils';

export interface SystemSettings {
  auditLogRetentionDays: number;
  idleTimeoutMinutes: number;
  maxSessionMinutes: number;
  updatedAt?: string | null;
}

export interface UpdateSystemSettingsRequest {
  auditLogRetentionDays: number;
  idleTimeoutMinutes: number;
  maxSessionMinutes: number;
}

@Injectable({ providedIn: 'root' })
export class SystemSettingsService {
  private readonly http = inject(HttpClient);
  private readonly _current = signal<SystemSettings | null>(null);
  readonly current = this._current.asReadonly();

  load(): Observable<SystemSettings> {
    return this.http.get<RawRecord>('/api/settings/system').pipe(
      map((r) => this.normalize(r)),
      tap((s) => this._current.set(s)),
    );
  }

  update(payload: UpdateSystemSettingsRequest): Observable<SystemSettings> {
    return this.http.put<RawRecord>('/api/settings/system', payload).pipe(
      map((r) => this.normalize(r)),
      tap((s) => this._current.set(s)),
    );
  }

  private normalize(raw: RawRecord): SystemSettings {
    return {
      auditLogRetentionDays: pickOr(raw, 0, 'auditLogRetentionDays', 'AuditLogRetentionDays'),
      idleTimeoutMinutes: pickOr(raw, 0, 'idleTimeoutMinutes', 'IdleTimeoutMinutes'),
      maxSessionMinutes: pickOr(raw, 0, 'maxSessionMinutes', 'MaxSessionMinutes'),
      updatedAt: pickOr(raw, null as string | null, 'updatedAt', 'UpdatedAt'),
    };
  }
}
