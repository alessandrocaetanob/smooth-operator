import {
  Component,
  OnInit,
  computed,
  inject,
  signal,
  ChangeDetectionStrategy,
} from '@angular/core';
import { switchMap } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { JsonPipe } from '@angular/common';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  SsoProviderType,
  SsoProviderView,
  SsoSettingsService,
  SsoTestResult,
  UpsertOidcRequest,
  UpsertSamlRequest,
} from '../../../services/sso-settings.service';
import { AuthService } from '../../../services/auth.service';

@Component({
  selector: 'app-sso-settings',
  imports: [FormsModule, JsonPipe, TranslatePipe],
  templateUrl: './sso.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './sso.css',
})
export class SsoSettings implements OnInit {
  private readonly svc = inject(SsoSettingsService);
  private readonly auth = inject(AuthService);
  private readonly translate = inject(TranslateService);

  readonly current = this.svc.current;
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly testBusy = signal(false);
  readonly toggleBusy = signal(false);
  readonly deleteBusy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly testResult = signal<SsoTestResult | null>(null);
  readonly confirmingDelete = signal(false);

  readonly type = signal<SsoProviderType>('Oidc');
  readonly name = signal('');

  // OIDC fields
  readonly oidcAuthority = signal('');
  readonly oidcClientId = signal('');
  readonly oidcClientSecret = signal('');
  readonly oidcScopes = signal('openid profile email');
  readonly oidcSubjectClaim = signal('sub');
  readonly oidcEmailClaim = signal('email');
  readonly oidcNameClaim = signal('name');

  // SAML fields
  readonly samlSpEntityId = signal('');
  readonly samlIdpEntityId = signal('');
  readonly samlIdpSsoUrl = signal('');
  readonly samlIdpCertificate = signal('');
  readonly samlSpCertificate = signal('');
  readonly samlSpPrivateKey = signal('');
  readonly samlNameIdFormat = signal('urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress');
  readonly samlAttributeEmail = signal('email');
  readonly samlAttributeName = signal('name');
  readonly samlWantAssertionsSigned = signal(true);
  readonly samlWantResponseSigned = signal(true);

  readonly metadataUrl = computed(() => `${globalThis.location.origin}/api/auth/sso/metadata`);
  readonly callbackUrl = computed(() => `${globalThis.location.origin}/api/auth/sso/callback`);
  readonly acsUrl = computed(() => `${globalThis.location.origin}/api/auth/sso/acs`);
  readonly hasProvider = computed(() => !!this.current()?.type);
  readonly providerLabel = computed(() => {
    const c = this.current();
    if (!c?.type) return this.translate.instant('pages.settingsSso.status.notConfigured');
    return `${c.name} (${c.type})`;
  });

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.svc.load().subscribe({
      next: (p) => {
        this.loading.set(false);
        this.applyToForm(p);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(
          this.toMessage(err) || this.translate.instant('pages.settingsSso.messages.loadError'),
        );
      },
    });
  }

  private applyToForm(p: SsoProviderView): void {
    if (p.type === 'Oidc' || p.type === 'Saml') {
      this.type.set(p.type);
    }
    this.name.set(p.name ?? '');
    if (p.oidc) {
      this.oidcAuthority.set(p.oidc.authority);
      this.oidcClientId.set(p.oidc.clientId);
      this.oidcScopes.set(p.oidc.scopes || 'openid profile email');
      this.oidcSubjectClaim.set(p.oidc.subjectClaim || 'sub');
      this.oidcEmailClaim.set(p.oidc.emailClaim || 'email');
      this.oidcNameClaim.set(p.oidc.nameClaim || 'name');
      this.oidcClientSecret.set('');
    }
    if (p.saml) {
      this.samlSpEntityId.set(p.saml.spEntityId);
      this.samlIdpEntityId.set(p.saml.idpEntityId);
      this.samlIdpSsoUrl.set(p.saml.idpSsoUrl);
      this.samlIdpCertificate.set(p.saml.idpCertificate);
      this.samlSpCertificate.set(p.saml.spCertificate);
      this.samlNameIdFormat.set(p.saml.nameIdFormat);
      this.samlAttributeEmail.set(p.saml.attributeEmail || 'email');
      this.samlAttributeName.set(p.saml.attributeName || 'name');
      this.samlWantAssertionsSigned.set(p.saml.wantAssertionsSigned);
      this.samlWantResponseSigned.set(p.saml.wantResponseSigned);
      this.samlSpPrivateKey.set('');
    }
  }

  save(): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.message.set(null);
    this.error.set(null);
    this.testResult.set(null);

    const obs =
      this.type() === 'Oidc'
        ? this.svc.upsertOidc(this.buildOidc())
        : this.svc.upsertSaml(this.buildSaml());

    obs
      .pipe(
        switchMap(() => {
          this.message.set(this.translate.instant('pages.settingsSso.messages.saveSuccess'));
          return this.svc.load();
        }),
        switchMap((p) => {
          this.applyToForm(p);
          return this.auth.loadSetupStatus();
        }),
      )
      .subscribe({
        next: () => this.busy.set(false),
        error: (err) => {
          this.busy.set(false);
          this.error.set(
            this.toMessage(err) || this.translate.instant('pages.settingsSso.messages.saveError'),
          );
        },
      });
  }

  private buildOidc(): UpsertOidcRequest {
    const secret = this.oidcClientSecret();
    return {
      name: this.name().trim(),
      authority: this.oidcAuthority().trim(),
      clientId: this.oidcClientId().trim(),
      clientSecret: secret.length > 0 ? secret : null,
      scopes: this.oidcScopes().trim() || 'openid profile email',
      subjectClaim: this.oidcSubjectClaim().trim() || 'sub',
      emailClaim: this.oidcEmailClaim().trim() || 'email',
      nameClaim: this.oidcNameClaim().trim() || 'name',
    };
  }

  private buildSaml(): UpsertSamlRequest {
    const key = this.samlSpPrivateKey();
    return {
      name: this.name().trim(),
      spEntityId: this.samlSpEntityId().trim(),
      idpEntityId: this.samlIdpEntityId().trim(),
      idpSsoUrl: this.samlIdpSsoUrl().trim(),
      idpCertificate: this.samlIdpCertificate().trim(),
      spCertificate: this.samlSpCertificate().trim(),
      spPrivateKey: key.length > 0 ? key : null,
      nameIdFormat: this.samlNameIdFormat().trim(),
      attributeEmail: this.samlAttributeEmail().trim() || 'email',
      attributeName: this.samlAttributeName().trim() || 'name',
      wantAssertionsSigned: this.samlWantAssertionsSigned(),
      wantResponseSigned: this.samlWantResponseSigned(),
    };
  }

  toggle(enable: boolean): void {
    if (this.toggleBusy()) return;
    this.toggleBusy.set(true);
    this.message.set(null);
    this.error.set(null);
    this.svc.toggle(enable).subscribe({
      next: () => {
        this.svc.load().subscribe({
          next: () => {
            this.toggleBusy.set(false);
            this.message.set(
              this.translate.instant(
                enable
                  ? 'pages.settingsSso.messages.toggleOnSuccess'
                  : 'pages.settingsSso.messages.toggleOffSuccess',
              ),
            );
            this.auth.loadSetupStatus().subscribe();
          },
          error: () => this.toggleBusy.set(false),
        });
      },
      error: (err) => {
        this.toggleBusy.set(false);
        this.error.set(
          this.toMessage(err) || this.translate.instant('pages.settingsSso.messages.toggleError'),
        );
      },
    });
  }

  remove(): void {
    if (this.deleteBusy()) return;
    this.deleteBusy.set(true);
    this.confirmingDelete.set(false);
    this.message.set(null);
    this.error.set(null);
    this.svc.remove().subscribe({
      next: () => {
        this.svc.load().subscribe({
          next: (p) => {
            this.deleteBusy.set(false);
            this.applyToForm(p);
            this.message.set(this.translate.instant('pages.settingsSso.messages.deleteSuccess'));
            this.auth.loadSetupStatus().subscribe();
          },
          error: () => this.deleteBusy.set(false),
        });
      },
      error: (err) => {
        this.deleteBusy.set(false);
        this.error.set(
          this.toMessage(err) || this.translate.instant('pages.settingsSso.messages.deleteError'),
        );
      },
    });
  }

  runTest(): void {
    if (this.testBusy()) return;
    this.testBusy.set(true);
    this.message.set(null);
    this.error.set(null);
    this.testResult.set(null);
    this.svc.test().subscribe({
      next: (r) => {
        this.testBusy.set(false);
        this.testResult.set(r);
      },
      error: (err) => {
        this.testBusy.set(false);
        const msg = this.toMessage(err);
        this.testResult.set({
          success: false,
          message: msg || this.translate.instant('pages.settingsSso.messages.testError'),
        });
      },
    });
  }

  copyMetadataUrl(): void {
    navigator.clipboard?.writeText(this.metadataUrl()).then(
      () =>
        this.message.set(this.translate.instant('pages.settingsSso.messages.copyMetadataSuccess')),
      () => this.error.set(this.translate.instant('pages.settingsSso.messages.copyError')),
    );
  }

  copyCallbackUrl(): void {
    navigator.clipboard?.writeText(this.callbackUrl()).then(
      () =>
        this.message.set(this.translate.instant('pages.settingsSso.messages.copyCallbackSuccess')),
      () => this.error.set(this.translate.instant('pages.settingsSso.messages.copyError')),
    );
  }

  copyAcsUrl(): void {
    navigator.clipboard?.writeText(this.acsUrl()).then(
      () => this.message.set(this.translate.instant('pages.settingsSso.messages.copyAcsSuccess')),
      () => this.error.set(this.translate.instant('pages.settingsSso.messages.copyError')),
    );
  }

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
