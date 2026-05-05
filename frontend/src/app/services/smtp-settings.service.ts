import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap, map } from 'rxjs';
import { pickOr, RawRecord } from './json-utils';

export interface SmtpSettings {
  configured: boolean;
  host: string;
  port: number;
  useSsl: boolean;
  username?: string | null;
  fromAddress: string;
  fromName?: string | null;
  enabled: boolean;
  hasPassword: boolean;
  updatedAt?: string | null;
}

export interface UpdateSmtpSettingsRequest {
  host: string;
  port: number;
  useSsl: boolean;
  username?: string | null;
  /** null = keep existing; "" = clear; non-empty = set new */
  password?: string | null;
  fromAddress: string;
  fromName?: string | null;
  enabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class SmtpSettingsService {
  private readonly http = inject(HttpClient);
  private readonly _current = signal<SmtpSettings | null>(null);
  readonly current = this._current.asReadonly();

  load(): Observable<SmtpSettings> {
    return this.http.get<RawRecord>('/api/settings/smtp').pipe(
      map((r) => this.normalize(r)),
      tap((s) => this._current.set(s)),
    );
  }

  update(payload: UpdateSmtpSettingsRequest): Observable<SmtpSettings> {
    return this.http.put<RawRecord>('/api/settings/smtp', payload).pipe(
      map((r) => this.normalize(r)),
      tap((s) => this._current.set(s)),
    );
  }

  test(toEmail: string): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>('/api/settings/smtp/test', { toEmail });
  }

  private normalize(raw: RawRecord): SmtpSettings {
    return {
      configured: pickOr(raw, false, 'configured', 'Configured'),
      host: pickOr(raw, '', 'host', 'Host'),
      port: pickOr(raw, 587, 'port', 'Port'),
      useSsl: pickOr(raw, true, 'useSsl', 'UseSsl'),
      username: pickOr(raw, null as string | null, 'username', 'Username'),
      fromAddress: pickOr(raw, '', 'fromAddress', 'FromAddress'),
      fromName: pickOr(raw, null as string | null, 'fromName', 'FromName'),
      enabled: pickOr(raw, true, 'enabled', 'Enabled'),
      hasPassword: pickOr(raw, false, 'hasPassword', 'HasPassword'),
      updatedAt: pickOr(raw, null as string | null, 'updatedAt', 'UpdatedAt'),
    };
  }
}
