import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface ApiTokenSummary {
  id: string;
  name: string;
  createdAt: string;
  expiresAt: string | null;
  lastUsedAt: string | null;
  revokedAt: string | null;
}

export interface CreateApiTokenRequest {
  name: string;
  expiresAt?: string | null;
}

export interface CreateApiTokenResult {
  id: string;
  name: string;
  plaintextToken: string;
  createdAt: string;
  expiresAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class ApiTokensService {
  private readonly http = inject(HttpClient);

  list(): Observable<ApiTokenSummary[]> {
    return this.http.get<ApiTokenSummary[]>('/api/api-tokens');
  }

  create(payload: CreateApiTokenRequest): Observable<CreateApiTokenResult> {
    return this.http.post<CreateApiTokenResult>('/api/api-tokens', payload);
  }

  revoke(id: string): Observable<void> {
    return this.http.delete<void>(`/api/api-tokens/${id}`);
  }
}
