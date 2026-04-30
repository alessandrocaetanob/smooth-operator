import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { pickOr } from './json-utils';

export type SsoProviderType = 'Oidc' | 'Saml';

export interface OidcConfigView {
  authority: string;
  clientId: string;
  scopes: string;
  subjectClaim: string;
  emailClaim: string;
  nameClaim: string;
  hasClientSecret: boolean;
}

export interface SamlConfigView {
  spEntityId: string;
  idpEntityId: string;
  idpSsoUrl: string;
  idpCertificate: string;
  spCertificate: string;
  nameIdFormat: string;
  attributeEmail: string;
  attributeName: string;
  wantAssertionsSigned: boolean;
  wantResponseSigned: boolean;
  hasSpPrivateKey: boolean;
}

export interface SsoProviderView {
  id?: string;
  name: string;
  /** Empty string means no provider configured yet. */
  type: SsoProviderType | '';
  isEnabled: boolean;
  createdAt?: string;
  updatedAt?: string;
  oidc?: OidcConfigView | null;
  saml?: SamlConfigView | null;
}

export interface UpsertOidcRequest {
  name: string;
  authority: string;
  clientId: string;
  /** Omit / null / empty = keep existing secret. */
  clientSecret?: string | null;
  scopes: string;
  subjectClaim: string;
  emailClaim: string;
  nameClaim: string;
}

export interface UpsertSamlRequest {
  name: string;
  spEntityId: string;
  idpEntityId: string;
  idpSsoUrl: string;
  idpCertificate: string;
  spCertificate: string;
  /** Omit / null / empty = keep existing private key. */
  spPrivateKey?: string | null;
  nameIdFormat: string;
  attributeEmail: string;
  attributeName: string;
  wantAssertionsSigned: boolean;
  wantResponseSigned: boolean;
}

export interface SsoTestResult {
  success: boolean;
  message: string;
  details?: unknown;
}

@Injectable({ providedIn: 'root' })
export class SsoSettingsService {
  private readonly http = inject(HttpClient);
  private readonly _current = signal<SsoProviderView | null>(null);
  readonly current = this._current.asReadonly();

  load(): Observable<SsoProviderView> {
    return this.http.get<any>('/api/settings/sso').pipe(
      map((r) => this.normalize(r)),
      tap((p) => this._current.set(p)),
    );
  }

  upsertOidc(req: UpsertOidcRequest): Observable<void> {
    return this.http.put<void>('/api/settings/sso/oidc', req);
  }

  upsertSaml(req: UpsertSamlRequest): Observable<void> {
    return this.http.put<void>('/api/settings/sso/saml', req);
  }

  toggle(enabled: boolean): Observable<void> {
    return this.http.post<void>('/api/settings/sso/toggle', { enabled });
  }

  remove(): Observable<void> {
    return this.http.delete<void>('/api/settings/sso');
  }

  test(): Observable<SsoTestResult> {
    return this.http.post<any>('/api/settings/sso/test', {}).pipe(
      map((r) => ({
        success: pickOr(r, false, 'success', 'Success'),
        message: pickOr(r, '', 'message', 'Message'),
        details: r?.details ?? r?.Details ?? null,
      })),
    );
  }

  private normalize(raw: any): SsoProviderView {
    const oidcRaw = raw?.oidc ?? raw?.Oidc;
    const samlRaw = raw?.saml ?? raw?.Saml;
    return {
      id: pickOr(raw, undefined as string | undefined, 'id', 'Id'),
      name: pickOr(raw, '', 'name', 'Name'),
      type: pickOr(raw, '' as SsoProviderType | '', 'type', 'Type'),
      isEnabled: pickOr(raw, false, 'isEnabled', 'IsEnabled'),
      createdAt: pickOr(raw, undefined as string | undefined, 'createdAt', 'CreatedAt'),
      updatedAt: pickOr(raw, undefined as string | undefined, 'updatedAt', 'UpdatedAt'),
      oidc: oidcRaw
        ? {
            authority: pickOr(oidcRaw, '', 'authority', 'Authority'),
            clientId: pickOr(oidcRaw, '', 'clientId', 'ClientId'),
            scopes: pickOr(oidcRaw, '', 'scopes', 'Scopes'),
            subjectClaim: pickOr(oidcRaw, 'sub', 'subjectClaim', 'SubjectClaim'),
            emailClaim: pickOr(oidcRaw, 'email', 'emailClaim', 'EmailClaim'),
            nameClaim: pickOr(oidcRaw, 'name', 'nameClaim', 'NameClaim'),
            hasClientSecret: pickOr(oidcRaw, false, 'hasClientSecret', 'HasClientSecret'),
          }
        : null,
      saml: samlRaw
        ? {
            spEntityId: pickOr(samlRaw, '', 'spEntityId', 'SpEntityId'),
            idpEntityId: pickOr(samlRaw, '', 'idpEntityId', 'IdpEntityId'),
            idpSsoUrl: pickOr(samlRaw, '', 'idpSsoUrl', 'IdpSsoUrl'),
            idpCertificate: pickOr(samlRaw, '', 'idpCertificate', 'IdpCertificate'),
            spCertificate: pickOr(samlRaw, '', 'spCertificate', 'SpCertificate'),
            nameIdFormat: pickOr(samlRaw, '', 'nameIdFormat', 'NameIdFormat'),
            attributeEmail: pickOr(samlRaw, '', 'attributeEmail', 'AttributeEmail'),
            attributeName: pickOr(samlRaw, '', 'attributeName', 'AttributeName'),
            wantAssertionsSigned: pickOr(
              samlRaw,
              true,
              'wantAssertionsSigned',
              'WantAssertionsSigned',
            ),
            wantResponseSigned: pickOr(samlRaw, true, 'wantResponseSigned', 'WantResponseSigned'),
            hasSpPrivateKey: pickOr(samlRaw, false, 'hasSpPrivateKey', 'HasSpPrivateKey'),
          }
        : null,
    };
  }
}
