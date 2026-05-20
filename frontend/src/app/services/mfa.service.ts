import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';

export interface MfaStatus {
  isEnabled: boolean;
  recoveryCodesRemaining: number;
}

export interface MfaEnrollResult {
  secretBase32: string;
  otpAuthUri: string;
}

export interface MfaConfirmResult {
  recoveryCodes: string[];
}

@Injectable({ providedIn: 'root' })
export class MfaService {
  private readonly http = inject(HttpClient);

  getStatus(): Observable<MfaStatus> {
    return this.http.get<Record<string, unknown>>('/api/mfa/status').pipe(
      map((raw) => ({
        isEnabled: (raw['isEnabled'] ?? raw['IsEnabled'] ?? false) as boolean,
        recoveryCodesRemaining: (raw['recoveryCodesRemaining'] ??
          raw['RecoveryCodesRemaining'] ??
          0) as number,
      })),
    );
  }

  enroll(): Observable<MfaEnrollResult> {
    return this.http.post<Record<string, unknown>>('/api/mfa/enroll', {}).pipe(
      map((raw) => ({
        secretBase32: (raw['secretBase32'] ?? raw['SecretBase32'] ?? '') as string,
        otpAuthUri: (raw['otpAuthUri'] ?? raw['OtpAuthUri'] ?? '') as string,
      })),
    );
  }

  confirm(code: string): Observable<MfaConfirmResult> {
    return this.http.post<Record<string, unknown>>('/api/mfa/confirm', { code }).pipe(
      map((raw) => ({
        recoveryCodes: (raw['recoveryCodes'] ?? raw['RecoveryCodes'] ?? []) as string[],
      })),
    );
  }

  disable(password: string): Observable<void> {
    return this.http.post<void>('/api/mfa/disable', { password });
  }
}
