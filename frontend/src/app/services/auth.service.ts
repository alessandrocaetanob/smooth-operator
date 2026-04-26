import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, of, catchError } from 'rxjs';

export interface Providers {
  local: boolean;
  entraId: boolean;
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
  linkedToEntra: boolean;
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
  private readonly _token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  private readonly _setup = signal<SetupStatus>({
    requiresSetup: false,
    providers: { local: true, entraId: false },
  });
  private readonly _user = signal<UserInfo | null>(this.userFromStoredToken(this._token()));

  readonly token = this._token.asReadonly();
  readonly setupStatus = this._setup.asReadonly();
  readonly providers = computed(() => this._setup().providers);
  readonly requiresSetup = computed(() => this._setup().requiresSetup);
  readonly currentUser = this._user.asReadonly();
  readonly isAuthenticated = computed(() => !!this._token());

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
    return this.http.get<any>('/api/auth/setup-status').pipe(
      tap((raw) => {
        if (!raw || typeof raw !== 'object') {
          throw new Error('Invalid setup-status response');
        }
        this._setup.set(this.normalizeStatus(raw));
      }),
      catchError(() => {
        // Backend unreachable or returned a non-JSON payload (e.g. SPA HTML
        // when the API proxy is misconfigured). Assume setup is required so
        // the operator can finish bootstrap instead of being stuck on login.
        const fallback: SetupStatus = {
          requiresSetup: true,
          providers: { local: true, entraId: false },
        };
        this._setup.set(fallback);
        return of(fallback);
      }),
    );
  }

  setup(payload: { name: string; email: string; password: string }): Observable<AuthResponse> {
    return this.http.post<any>('/api/auth/setup', payload).pipe(tap((res) => this.acceptAuth(res)));
  }

  login(payload: { email: string; password: string }): Observable<AuthResponse> {
    return this.http.post<any>('/api/auth/login', payload).pipe(tap((res) => this.acceptAuth(res)));
  }

  me(): Observable<UserInfo> {
    return this.http
      .get<any>('/api/auth/me')
      .pipe(tap((u) => this._user.set(this.normalizeUser(u, this._token()))));
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

  private acceptAuth(raw: any): AuthResponse {
    const token = raw.token ?? raw.Token;
    const expiresAt = raw.expiresAt ?? raw.ExpiresAt;
    const user = this.normalizeUser(raw.user ?? raw.User, token);
    localStorage.setItem(TOKEN_KEY, token);
    this._token.set(token);
    this._user.set(user);
    // After bootstrap the next visitor should NOT see the setup screen.
    this._setup.update((s) => ({ ...s, requiresSetup: false }));
    return { token, expiresAt, user };
  }

  private normalizeStatus(raw: any): SetupStatus {
    const providers = raw.providers ?? raw.Providers ?? {};
    return {
      requiresSetup: raw.requiresSetup ?? raw.RequiresSetup ?? false,
      providers: {
        local: providers.local ?? providers.Local ?? true,
        entraId: providers.entraId ?? providers.EntraId ?? false,
      },
    };
  }

  private normalizeUser(raw: any, fallbackToken: string | null = null): UserInfo {
    const roles = this.normalizeRoles(
      raw?.roles ?? raw?.Roles ?? this.rolesFromToken(fallbackToken),
    );
    return {
      id: raw?.id ?? raw?.Id ?? '',
      email: raw?.email ?? raw?.Email ?? '',
      name: raw?.name ?? raw?.Name ?? '',
      hasPassword: raw?.hasPassword ?? raw?.HasPassword ?? false,
      linkedToEntra: raw?.linkedToEntra ?? raw?.LinkedToEntra ?? false,
      avatarUrl: raw?.avatarUrl ?? raw?.AvatarUrl ?? null,
      roles,
    };
  }

  setCurrentUser(user: UserInfo): void {
    this._user.set(user);
  }

  private userFromStoredToken(token: string | null): UserInfo | null {
    if (!token) return null;
    const payload = this.jwtPayload(token);
    if (!payload) return null;

    const roleClaim =
      payload['role'] ?? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];

    return {
      id:
        payload['nameid'] ??
        payload['sub'] ??
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ??
        '',
      email:
        payload['email'] ??
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ??
        '',
      name:
        payload['unique_name'] ??
        payload['name'] ??
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ??
        payload['email'] ??
        '',
      hasPassword: false,
      linkedToEntra: false,
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

  private jwtPayload(token: string): Record<string, any> | null {
    const parts = token.split('.');
    if (parts.length < 2) return null;
    const segment = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = segment.padEnd(segment.length + ((4 - (segment.length % 4)) % 4), '=');
    try {
      return JSON.parse(atob(padded));
    } catch {
      return null;
    }
  }

  private normalizeRoles(raw: any): string[] {
    const values = Array.isArray(raw) ? raw : raw ? [raw] : [];
    const clean = values.map((r) => String(r).trim()).filter((r) => r.length > 0);
    return Array.from(new Set(clean));
  }
}
