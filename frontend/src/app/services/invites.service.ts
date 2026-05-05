import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { pickOr, RawRecord } from './json-utils';

export interface InvitePreview {
  email: string;
  name: string;
  type: string;
}

@Injectable({ providedIn: 'root' })
export class InvitesService {
  private readonly http = inject(HttpClient);

  preview(token: string): Observable<InvitePreview> {
    return this.http.get<RawRecord>(`/api/invites/${encodeURIComponent(token)}`).pipe(
      map((raw) => ({
        email: pickOr(raw, '', 'email', 'Email'),
        name: pickOr(raw, '', 'name', 'Name'),
        type: pickOr(raw, 'user_invite', 'type', 'Type'),
      })),
    );
  }

  redeem(token: string, payload: { password: string; name?: string }): Observable<void> {
    return this.http.post<void>(`/api/invites/${encodeURIComponent(token)}/redeem`, payload);
  }
}
