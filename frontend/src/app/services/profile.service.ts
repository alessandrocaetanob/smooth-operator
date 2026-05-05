import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { AuthService, UserInfo } from './auth.service';
import { RawRecord } from './json-utils';

export interface UpdateProfilePayload {
  name: string;
  avatarBase64?: string | null;
  avatarMimeType?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  updateProfile(payload: UpdateProfilePayload): Observable<UserInfo> {
    const body: Record<string, unknown> = { Name: payload.name };
    if (payload.avatarBase64) {
      body['AvatarBase64'] = payload.avatarBase64;
      body['AvatarMimeType'] = payload.avatarMimeType;
    }
    return this.http.put<RawRecord>('/api/users/me/profile', body).pipe(
      map((raw) => {
        const u = this.normalize(raw);
        this.auth.setCurrentUser(u);
        return u;
      }),
    );
  }

  removeAvatar(): Observable<UserInfo> {
    return this.http.delete<RawRecord>('/api/users/me/avatar').pipe(
      map((raw) => {
        const u = this.normalize(raw);
        this.auth.setCurrentUser(u);
        return u;
      }),
    );
  }

  private normalize(raw: RawRecord): UserInfo {
    return {
      id: (raw?.['id'] ?? raw?.['Id'] ?? '') as string,
      email: (raw?.['email'] ?? raw?.['Email'] ?? '') as string,
      name: (raw?.['name'] ?? raw?.['Name'] ?? '') as string,
      hasPassword: (raw?.['hasPassword'] ?? raw?.['HasPassword'] ?? false) as boolean,
      ssoLinked: (raw?.['ssoLinked'] ?? raw?.['SsoLinked'] ?? false) as boolean,
      ssoProviderType: (raw?.['ssoProviderType'] ?? raw?.['SsoProviderType'] ?? null) as
        | string
        | null,
      avatarUrl: (raw?.['avatarUrl'] ?? raw?.['AvatarUrl'] ?? null) as string | null,
      roles: Array.isArray(raw?.['roles'] ?? raw?.['Roles'])
        ? ((raw?.['roles'] ?? raw?.['Roles']) as string[])
        : [],
    };
  }
}
