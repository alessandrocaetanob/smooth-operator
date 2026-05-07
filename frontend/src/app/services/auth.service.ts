import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, of, catchError, map } from 'rxjs';

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

const TOKEN_KEY = 'smooth-operator.token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  // Backend returns PascalCase by default (no JsonOptions configured), so we
  // normalise on read instead of forcing the server to camelCase.
  private readonly _token = signal<string | null>(this.readStoredToken());
  private readonly _setup = signal<SetupStatus>({
    requiresSetup: false,
    providers: { local: true, sso: null },
  });
  private readonly _user = signal<UserInfo | null>(this.userFromStoredToken(this._token()));

  readonly token = this._token.asReadonly();
  readonly setupStatus = this._setup.asReadonly();
  readonly providers = computed(() => this._setup().providers);
  readonly requiresSetup = computed(() => this._setup().requiresSetup);
  readonly currentUser = this._user.asReadonly();
  readonly isAuthenticated = computed(() => {
    const t = this._token();
    return !!t && !this.isJwtExpired(t);
  });

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

  login(payload: { email: string; password: string }): Observable<AuthResponse> {
    return this.http
      .post<Record<string, unknown>>('/api/auth/login', payload)
      .pipe(map((res) => this.acceptAuth(res)));
  }

  me(): Observable<UserInfo> {
    return this.http.get<Record<string, unknown>>('/api/auth/me').pipe(
      map((u) => {
        this._user.set(this.normalizeUser(u, this._token()));
        return this.normalizeUser(u, this._token());
      }),
    );
  }

  hasRole(roleName: string): boolean {
    return this.currentRoles().some((r) => r.toLowerCase() === roleName.toLowerCase());
  }

  hasAnyRole(...roleNames: string[]): boolean {
    const roleSet = new Set(this.currentRoles().map((r) => r.toLowerCase()));
    return roleNames.some((roleName) => roleSet.has(roleName.toLowerCase()));
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    this._token.set(null);
    this._user.set(null);
  }

  private currentRoles(): string[] {
    return this._user()?.roles ?? this.rolesFromToken(this._token());
  }

  private acceptAuth(raw: Record<string, unknown>): AuthResponse {
    const token = (raw['token'] ?? raw['Token']) as string;
    const expiresAt = (raw['expiresAt'] ?? raw['ExpiresAt']) as string;
    const user = this.normalizeUser(
      (raw['user'] ?? raw['User']) as Record<string, unknown> | null | undefined,
      token,
    );
    localStorage.setItem(TOKEN_KEY, token);
    this._token.set(token);
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
                type: ssoType as 'Oidc' | 'Saml',
                name: (ssoName as string | null) ?? 'Single Sign-On',
              }
            : null,
      },
    };
  }

  private normalizeUser(
    raw: Record<string, unknown> | null | undefined,
    fallbackToken: string | null = null,
  ): UserInfo {
    const roles = this.normalizeRoles(
      raw?.['roles'] ?? raw?.['Roles'] ?? this.rolesFromToken(fallbackToken),
    );
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
      roles,
    };
  }

  setCurrentUser(user: UserInfo): void {
    this._user.set(user);
  }

  /**
   * Accepts a JWT minted by the backend after a successful SSO callback.
   * Persists it like a normal local-login token; caller is expected to
   * follow up with `me()` to populate the user signal.
   */
  acceptSsoToken(token: string): void {
    if (!token) return;
    localStorage.setItem(TOKEN_KEY, token);
    this._token.set(token);
    this._user.set(this.userFromStoredToken(token));
    this._setup.update((s) => ({ ...s, requiresSetup: false }));
  }

  private userFromStoredToken(token: string | null): UserInfo | null {
    if (!token) return null;
    const payload = this.jwtPayload(token);
    if (!payload) return null;

    const roleClaim =
      payload['role'] ?? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];

    return {
      id: (payload['nameid'] ??
        payload['sub'] ??
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ??
        '') as string,
      email: (payload['email'] ??
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ??
        '') as string,
      name: (payload['unique_name'] ??
        payload['name'] ??
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ??
        payload['email'] ??
        '') as string,
      hasPassword: false,
      ssoLinked: false,
      ssoProviderType: null,
      roles: this.normalizeRoles(roleClaim),
    };
  }

  private rolesFromToken(token: string | null): string[] {
    if (!token) return [];
    const payload = this.jwtPayload(token);
    return this.normalizeRoles(
      payload?.['role'] ??
        payload?.['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
    );
  }

  private jwtPayload(token: string): Record<string, unknown> | null {
    const parts = token.split('.');
    if (parts.length < 2) return null;
    const segment = parts[1].replaceAll(/-/g, '+').replaceAll(/_/g, '/');
    const padded = segment.padEnd(segment.length + ((4 - (segment.length % 4)) % 4), '=');
    try {
      return JSON.parse(atob(padded));
    } catch {
      return null;
    }
  }

  private isJwtExpired(token: string): boolean {
    const payload = this.jwtPayload(token);
    if (!payload || typeof payload['exp'] !== 'number') return true;
    // Treat 5s before server expiry as already expired so guards fail-fast
    // instead of letting an about-to-die token through.
    return payload['exp'] * 1000 < Date.now() + 5_000;
  }

  private readStoredToken(): string | null {
    const t = localStorage.getItem(TOKEN_KEY);
    if (!t) return null;
    if (this.isJwtExpired(t)) {
      localStorage.removeItem(TOKEN_KEY);
      return null;
    }
    return t;
  }

  private normalizeRoles(raw: unknown): string[] {
    const values = Array.isArray(raw) ? raw : raw ? [raw] : [];
    const clean = values.map((r) => String(r).trim()).filter((r) => r.length > 0);
    return Array.from(new Set(clean));
  }
}
