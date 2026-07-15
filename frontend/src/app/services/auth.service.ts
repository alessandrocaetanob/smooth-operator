import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, of, catchError, map, EMPTY } from 'rxjs';

export interface SsoInfo {
  enabled: boolean;
  type: 'Oidc' | 'Saml';
  name: string;
}

export interface Providers {
  local: boolean;
  sso: SsoInfo | null;
}

export interface SetupStatus {
  requiresSetup: boolean;
  providers: Providers;
}

export interface UserInfo {
  id: string;
  email: string;
  name: string;
  hasPassword: boolean;
  ssoLinked: boolean;
  ssoProviderType: string | null;
  avatarUrl?: string | null;
  roles: string[];
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: UserInfo;
}

export interface MfaChallenge {
  mfaRequired: true;
  challengeToken: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  // Backend returns PascalCase by default (no JsonOptions configured), so we
  // normalise on read instead of forcing the server to camelCase.
  private readonly _setup = signal<SetupStatus>({
    requiresSetup: false,
    providers: { local: true, sso: null },
  });
  private readonly _user = signal<UserInfo | null>(null);

  readonly setupStatus = this._setup.asReadonly();
  readonly providers = computed(() => this._setup().providers);
  readonly requiresSetup = computed(() => this._setup().requiresSetup);
  readonly currentUser = this._user.asReadonly();
  readonly isAuthenticated = computed(() => this._user() !== null);

  private readonly _roleSet = computed(
    () => new Set(this._user()?.roles.map((r) => r.toLowerCase()) ?? []),
  );

  readonly isOwner = computed(() => this.hasRole('Owner'));
  readonly isOwnerOrAdmin = computed(() => this.hasAnyRole('Owner', 'Admin'));
  readonly isTeamAdmin = computed(() => this.hasRole('TeamAdmin'));
  readonly canManageUsers = computed(() => this.isOwnerOrAdmin());
  readonly canInviteUsers = computed(() => this.isOwnerOrAdmin());
  readonly canManageVaults = computed(() => this.isOwnerOrAdmin());
  readonly canManageCredentials = computed(() => this.isOwnerOrAdmin());
  readonly canManageConnections = computed(() => this.hasAnyRole('Owner', 'Admin', 'TeamAdmin'));
  readonly canViewCredentials = computed(() => this.hasAnyRole('Owner', 'Admin', 'TeamAdmin'));
  readonly canAccessSettings = computed(() => this.isOwnerOrAdmin());

  loadSetupStatus(): Observable<SetupStatus> {
    return this.http.get<Record<string, unknown>>('/api/auth/setup-status').pipe(
      map((raw) => {
        if (!raw || typeof raw !== 'object') {
          throw new Error('Invalid setup-status response');
        }
        return this.normalizeStatus(raw);
      }),
      tap((status) => this._setup.set(status)),
      catchError(() => {
        const fallback: SetupStatus = {
          requiresSetup: false,
          providers: { local: true, sso: null },
        };
        this._setup.set(fallback);
        return of(fallback);
      }),
    );
  }

  setup(payload: { name: string; email: string; password: string }): Observable<AuthResponse> {
    return this.http
      .post<Record<string, unknown>>('/api/auth/setup', payload)
      .pipe(map((res) => this.acceptAuth(res)));
  }

  login(payload: { email: string; password: string }): Observable<AuthResponse | MfaChallenge> {
    return this.http.post<Record<string, unknown>>('/api/auth/login', payload).pipe(
      map((res) => {
        const mfaRequired = (res['mfaRequired'] ?? res['MfaRequired']) as boolean | undefined;
        if (mfaRequired) {
          return {
            mfaRequired: true as const,
            challengeToken: (res['challengeToken'] ?? res['ChallengeToken'] ?? '') as string,
          };
        }
        return this.acceptAuth(res);
      }),
    );
  }

  verifyMfa(challengeToken: string, code: string): Observable<AuthResponse> {
    return this.http
      .post<Record<string, unknown>>('/api/auth/mfa/verify', { challengeToken, code })
      .pipe(map((res) => this.acceptAuth(res)));
  }

  me(): Observable<UserInfo> {
    return this.http.get<Record<string, unknown>>('/api/auth/me').pipe(
      map((u) => {
        const user = this.normalizeUser(u);
        this._user.set(user);
        return user;
      }),
    );
  }

  hasRole(roleName: string): boolean {
    return this._roleSet().has(roleName.toLowerCase());
  }

  hasAnyRole(...roleNames: string[]): boolean {
    const set = this._roleSet();
    return roleNames.some((r) => set.has(r.toLowerCase()));
  }

  logout(): void {
    if (!this._user()) return;
    this._user.set(null);
    this.http
      .post<void>('/api/auth/logout', {})
      .pipe(catchError(() => EMPTY))
      .subscribe();
  }

  private acceptAuth(raw: Record<string, unknown>): AuthResponse {
    const token = (raw['token'] ?? raw['Token']) as string;
    const expiresAt = (raw['expiresAt'] ?? raw['ExpiresAt']) as string;
    const user = this.normalizeUser(
      (raw['user'] ?? raw['User']) as Record<string, unknown> | null | undefined,
    );
    this._user.set(user);
    // After bootstrap the next visitor should NOT see the setup screen.
    this._setup.update((s) => ({ ...s, requiresSetup: false }));
    return { token, expiresAt, user };
  }

  private normalizeStatus(raw: Record<string, unknown>): SetupStatus {
    const providers = (raw['providers'] ?? raw['Providers'] ?? {}) as Record<string, unknown>;
    const ssoEnabled = providers['sso'] ?? providers['Sso'] ?? false;
    const ssoType = providers['ssoType'] ?? providers['SsoType'] ?? null;
    const ssoName = providers['ssoName'] ?? providers['SsoName'] ?? null;
    return {
      requiresSetup: (raw['requiresSetup'] ?? raw['RequiresSetup'] ?? false) as boolean,
      providers: {
        local: (providers['local'] ?? providers['Local'] ?? true) as boolean,
        sso:
          ssoEnabled && (ssoType === 'Oidc' || ssoType === 'Saml')
            ? {
                enabled: true,
                type: ssoType,
                name: (ssoName as string | null) ?? 'Single Sign-On',
              }
            : null,
      },
    };
  }

  private normalizeUser(raw: Record<string, unknown> | null | undefined): UserInfo {
    const roles = this.normalizeRoles(raw?.['roles'] ?? raw?.['Roles']);
    return {
      id: (raw?.['id'] ?? raw?.['Id'] ?? '') as string,
      email: (raw?.['email'] ?? raw?.['Email'] ?? '') as string,
      name: (raw?.['name'] ?? raw?.['Name'] ?? '') as string,
      hasPassword: (raw?.['hasPassword'] ?? raw?.['HasPassword'] ?? false) as boolean,
      ssoLinked: (raw?.['ssoLinked'] ?? raw?.['SsoLinked'] ?? false) as boolean,
      ssoProviderType: (raw?.['ssoProviderType'] ?? raw?.['SsoProviderType'] ?? null) as
        string | null,
      avatarUrl: (raw?.['avatarUrl'] ?? raw?.['AvatarUrl'] ?? null) as string | null,
      roles,
    };
  }

  setCurrentUser(user: UserInfo): void {
    this._user.set(user);
  }

  private normalizeRoles(raw: unknown): string[] {
    let values: unknown[];
    if (Array.isArray(raw)) {
      values = raw;
    } else if (raw) {
      values = [raw];
    } else {
      values = [];
    }
    const clean = values.map((r) => String(r).trim()).filter((r) => r.length > 0);
    return Array.from(new Set(clean));
  }
}
