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
  private readonly _user = signal<UserInfo | null>(null);

  readonly token = this._token.asReadonly();
  readonly setupStatus = this._setup.asReadonly();
  readonly providers = computed(() => this._setup().providers);
  readonly requiresSetup = computed(() => this._setup().requiresSetup);
  readonly currentUser = this._user.asReadonly();
  readonly isAuthenticated = computed(() => !!this._token());

  loadSetupStatus(): Observable<SetupStatus> {
    return this.http.get<any>('/api/auth/setup-status').pipe(
      tap((raw) => this._setup.set(this.normalizeStatus(raw))),
      catchError(() => {
        // Backend unreachable – assume defaults so the UI still renders.
        const fallback: SetupStatus = {
          requiresSetup: false,
          providers: { local: true, entraId: false },
        };
        this._setup.set(fallback);
        return of(fallback);
      }),
    );
  }

  setup(payload: { name: string; email: string; password: string }): Observable<AuthResponse> {
    return this.http
      .post<any>('/api/auth/setup', payload)
      .pipe(tap((res) => this.acceptAuth(res)));
  }

  login(payload: { email: string; password: string }): Observable<AuthResponse> {
    return this.http
      .post<any>('/api/auth/login', payload)
      .pipe(tap((res) => this.acceptAuth(res)));
  }

  me(): Observable<UserInfo> {
    return this.http
      .get<any>('/api/auth/me')
      .pipe(tap((u) => this._user.set(this.normalizeUser(u))));
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    this._token.set(null);
    this._user.set(null);
  }

  private acceptAuth(raw: any): AuthResponse {
    const token = raw.token ?? raw.Token;
    const expiresAt = raw.expiresAt ?? raw.ExpiresAt;
    const user = this.normalizeUser(raw.user ?? raw.User);
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

  private normalizeUser(raw: any): UserInfo {
    return {
      id: raw.id ?? raw.Id,
      email: raw.email ?? raw.Email,
      name: raw.name ?? raw.Name,
      hasPassword: raw.hasPassword ?? raw.HasPassword ?? false,
      linkedToEntra: raw.linkedToEntra ?? raw.LinkedToEntra ?? false,
    };
  }
}
