import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { AuthService, UserInfo } from './auth.service';

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
    return this.http
      .put<any>('/api/users/me/profile', body)
      .pipe(tap((raw) => this.auth.setCurrentUser(this.normalize(raw))));
  }

  removeAvatar(): Observable<UserInfo> {
    return this.http
      .delete<any>('/api/users/me/avatar')
      .pipe(tap((raw) => this.auth.setCurrentUser(this.normalize(raw))));
  }

  private normalize(raw: any): UserInfo {
    return {
      id: raw?.id ?? raw?.Id ?? '',
      email: raw?.email ?? raw?.Email ?? '',
      name: raw?.name ?? raw?.Name ?? '',
      hasPassword: raw?.hasPassword ?? raw?.HasPassword ?? false,
      ssoLinked: raw?.ssoLinked ?? raw?.SsoLinked ?? false,
      ssoProviderType: raw?.ssoProviderType ?? raw?.SsoProviderType ?? null,
      avatarUrl: raw?.avatarUrl ?? raw?.AvatarUrl ?? null,
      roles: Array.isArray(raw?.roles ?? raw?.Roles) ? (raw?.roles ?? raw?.Roles) : [],
    };
  }
}
