import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { Profile } from './profile';
import { ProfileService } from '../../services/profile.service';
import { AuthService } from '../../services/auth.service';
import { MfaService } from '../../services/mfa.service';
import { ApiTokensService } from '../../services/api-tokens.service';

vi.mock('qrcode', () => ({
  toDataURL: vi.fn(() => Promise.resolve('data:image/png;base64,QR')),
}));
import { toDataURL as qrToDataURL } from 'qrcode';
// qrcode's `toDataURL` has callback overloads that confuse TS in tests;
// cast through unknown to the promise-returning shape for mock typing.
const qrMock = qrToDataURL as unknown as ReturnType<typeof vi.fn>;

describe('Profile', () => {
  let fixture: ComponentFixture<Profile>;
  let component: Profile;
  let profileSvc: {
    updateProfile: ReturnType<typeof vi.fn>;
    removeAvatar: ReturnType<typeof vi.fn>;
  };
  let mfaSvc: {
    getStatus: ReturnType<typeof vi.fn>;
    enroll: ReturnType<typeof vi.fn>;
    confirm: ReturnType<typeof vi.fn>;
    disable: ReturnType<typeof vi.fn>;
  };
  let apiTokensSvc: {
    list: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    revoke: ReturnType<typeof vi.fn>;
  };
  let auth: {
    currentUser: ReturnType<
      typeof signal<{ id: string; name: string; avatarUrl?: string | null } | null>
    >;
  };

  async function build(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [Profile],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: ProfileService, useValue: profileSvc },
        { provide: AuthService, useValue: auth },
        { provide: MfaService, useValue: mfaSvc },
        { provide: ApiTokensService, useValue: apiTokensSvc },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      pages: {
        profile: {
          name: { required: 'Name is required.' },
          saved: 'Profile updated.',
          saveError: 'Could not save profile.',
          avatar: {
            invalidType: 'Avatar must be PNG, JPEG, or WebP.',
            tooLarge: 'Avatar exceeds 1 MB after downscaling. Pick a simpler image.',
            readError: 'Could not read that image.',
            removed: 'Avatar removed.',
            removeError: 'Could not remove avatar.',
          },
          mfa: {
            startError: 'Could not start MFA enrollment.',
            invalidCode: 'Invalid code. Try again.',
            disableError: 'Could not disable MFA.',
            recoveryFile: {
              title: 'Smooth Operator — MFA Recovery Codes',
              generated: 'Generated: {{date}}',
              instructions: 'Each code can be used once to sign in if you lose your authenticator.',
              storeSafely: 'Store this file somewhere safe and delete it after printing.',
            },
          },
          apiTokens: {
            createError: 'Could not create token.',
            revokeError: 'Could not revoke token.',
          },
        },
      },
    });
    translate.use('en');

    fixture = TestBed.createComponent(Profile);
    component = fixture.componentInstance;
  }

  beforeEach(async () => {
    profileSvc = {
      updateProfile: vi.fn(() => of(undefined)),
      removeAvatar: vi.fn(() => of(undefined)),
    };
    mfaSvc = {
      getStatus: vi.fn(() => of({ isEnabled: false, recoveryCodesRemaining: 0 })),
      enroll: vi.fn(() => of({ secretBase32: 'ABCD', otpAuthUri: 'otpauth://totp/x' })),
      confirm: vi.fn(() => of({ recoveryCodes: ['AAAA-BBBB', 'CCCC-DDDD'] })),
      disable: vi.fn(() => of(undefined)),
    };
    apiTokensSvc = {
      list: vi.fn(() => of([])),
      create: vi.fn(() =>
        of({
          id: 't1',
          name: 'ci-bot',
          plaintextToken: 'sop_lookup_secret',
          createdAt: '2026-05-20T00:00:00Z',
          expiresAt: null,
        }),
      ),
      revoke: vi.fn(() => of(undefined)),
    };
    auth = {
      currentUser: signal<{ id: string; name: string; avatarUrl?: string | null } | null>({
        id: 'u',
        name: 'Alice Wonder',
        avatarUrl: 'https://x/a.png',
      }),
    };
    qrMock.mockClear();
    qrMock.mockResolvedValue('data:image/png;base64,QR');
    await build();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('initials computed handles single-name', () => {
    auth.currentUser.set({ id: 'u', name: 'Bob' });
    expect(component.initials()).toBe('BO');
  });

  it('initials computed handles multi-name', () => {
    expect(component.initials()).toBe('AW');
  });

  it('initials returns ? when no name', () => {
    auth.currentUser.set(null);
    expect(component.initials()).toBe('?');
    auth.currentUser.set({ id: 'u', name: '' });
    expect(component.initials()).toBe('?');
  });

  it('currentAvatar prefers preview over user.avatarUrl', () => {
    component.previewUrl.set('preview://x');
    expect(component.currentAvatar()).toBe('preview://x');
    component.previewUrl.set(null);
    expect(component.currentAvatar()).toBe('https://x/a.png');
    auth.currentUser.set({ id: 'u', name: 'A' });
    expect(component.currentAvatar()).toBeNull();
  });

  it('canRemove respects pending and removing flags', () => {
    expect(component.canRemove()).toBe(true);
    component.pendingBase64.set('x');
    expect(component.canRemove()).toBe(false);
    component.pendingBase64.set(null);
    component.removing.set(true);
    expect(component.canRemove()).toBe(false);
  });

  it('canRemove is false when there is no avatar', () => {
    auth.currentUser.set({ id: 'u', name: 'a' });
    expect(component.canRemove()).toBe(false);
  });

  describe('save()', () => {
    it('rejects empty name', () => {
      component.name.set('   ');
      component.save();
      expect(component.error()).toBe('Name is required.');
      expect(profileSvc.updateProfile).not.toHaveBeenCalled();
    });

    it('calls updateProfile and sets success', () => {
      component.name.set('Alice Wonder');
      component.pendingBase64.set('B64');
      component.pendingMime.set('image/png');
      component.previewUrl.set('preview');
      component.save();
      expect(profileSvc.updateProfile).toHaveBeenCalledWith({
        name: 'Alice Wonder',
        avatarBase64: 'B64',
        avatarMimeType: 'image/png',
      });
      expect(component.success()).toBe('Profile updated.');
      expect(component.previewUrl()).toBeNull();
      expect(component.pendingBase64()).toBeNull();
    });

    it('surfaces backend error', () => {
      profileSvc.updateProfile.mockReturnValueOnce(
        throwError(() => ({ error: { error: 'too long' } })),
      );
      component.name.set('A');
      component.save();
      expect(component.error()).toBe('too long');
      expect(component.saving()).toBe(false);
    });

    it('falls back to default error', () => {
      profileSvc.updateProfile.mockReturnValueOnce(throwError(() => ({})));
      component.name.set('A');
      component.save();
      expect(component.error()).toBe('Could not save profile.');
    });
  });

  describe('cancelPending()', () => {
    it('clears preview and pending fields', () => {
      component.previewUrl.set('x');
      component.pendingBase64.set('y');
      component.pendingMime.set('image/png');
      component.cancelPending();
      expect(component.previewUrl()).toBeNull();
      expect(component.pendingBase64()).toBeNull();
      expect(component.pendingMime()).toBeNull();
    });
  });

  describe('removeAvatar()', () => {
    it('succeeds', () => {
      component.removeAvatar();
      expect(component.success()).toBe('Avatar removed.');
      expect(component.removing()).toBe(false);
    });

    it('handles error', () => {
      profileSvc.removeAvatar.mockReturnValueOnce(throwError(() => new Error('x')));
      component.removeAvatar();
      expect(component.error()).toBe('Could not remove avatar.');
      expect(component.removing()).toBe(false);
    });
  });

  describe('onFileSelected()', () => {
    function makeEvent(file: File | null): Event {
      const input = document.createElement('input');
      // jsdom HTMLInputElement: simulate the files list
      Object.defineProperty(input, 'files', {
        configurable: true,
        get: () => (file ? [file] : []),
      });
      return { target: input } as unknown as Event;
    }

    it('no-ops without file', async () => {
      await component.onFileSelected(makeEvent(null));
      expect(component.previewUrl()).toBeNull();
    });

    it('rejects unsupported mime type', async () => {
      const f = new File(['x'], 'a.gif', { type: 'image/gif' });
      await component.onFileSelected(makeEvent(f));
      expect(component.error()).toContain('PNG');
      expect(component.previewUrl()).toBeNull();
    });

    it('sets error when downscale fails', async () => {
      const f = new File(['x'], 'a.png', { type: 'image/png' });
      // Force FileReader to fail by mocking its readAsDataURL
      const origReader = globalThis.FileReader;
      class BrokenReader {
        result: string | null = null;
        onload: (() => void) | null = null;
        onerror: (() => void) | null = null;
        readAsDataURL() {
          setTimeout(() => this.onerror?.(), 0);
        }
      }
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      globalThis.FileReader = BrokenReader as any;
      try {
        await component.onFileSelected(makeEvent(f));
        expect(component.error()).toBe('Could not read that image.');
      } finally {
        globalThis.FileReader = origReader;
      }
    });
  });

  describe('MFA section', () => {
    it('loadMfaStatus is called on init and stores result', () => {
      expect(mfaSvc.getStatus).toHaveBeenCalled();
      expect(component.mfaStatus()).toEqual({ isEnabled: false, recoveryCodesRemaining: 0 });
    });

    it('loadMfaStatus on error falls back to disabled+zero', async () => {
      mfaSvc.getStatus.mockReturnValueOnce(throwError(() => new Error('boom')));
      TestBed.resetTestingModule();
      await build();
      expect(component.mfaStatus()).toEqual({ isEnabled: false, recoveryCodesRemaining: 0 });
    });

    describe('startMfaEnrollment()', () => {
      it('success transitions to setup step with secret + QR', async () => {
        component.startMfaEnrollment();
        await new Promise((r) => setTimeout(r, 0));
        expect(component.mfaEnrollStep()).toBe('setup');
        expect(component.mfaSecretBase32()).toBe('ABCD');
        expect(component.mfaQrDataUrl()).toBe('data:image/png;base64,QR');
        expect(component.mfaEnrolling()).toBe(false);
      });

      it('still transitions to setup if QR generation fails', async () => {
        qrMock.mockRejectedValueOnce(new Error('canvas'));
        component.startMfaEnrollment();
        await new Promise((r) => setTimeout(r, 0));
        expect(component.mfaEnrollStep()).toBe('setup');
        expect(component.mfaQrDataUrl()).toBeNull();
      });

      it('service error sets mfaError and clears enrolling flag', () => {
        mfaSvc.enroll.mockReturnValueOnce(
          throwError(() => ({ error: { message: 'enrollment closed' } })),
        );
        component.startMfaEnrollment();
        expect(component.mfaError()).toBe('enrollment closed');
        expect(component.mfaEnrolling()).toBe(false);
      });

      it('uses default error message when backend gives none', () => {
        mfaSvc.enroll.mockReturnValueOnce(throwError(() => ({})));
        component.startMfaEnrollment();
        expect(component.mfaError()).toBe('Could not start MFA enrollment.');
      });
    });

    describe('confirmMfaEnrollment()', () => {
      it('empty code is a no-op', () => {
        component.mfaConfirmCode.set('');
        component.confirmMfaEnrollment();
        expect(mfaSvc.confirm).not.toHaveBeenCalled();
      });

      it('success transitions to codes step + stores recovery codes', () => {
        component.mfaConfirmCode.set('123456');
        component.confirmMfaEnrollment();
        expect(mfaSvc.confirm).toHaveBeenCalledWith('123456');
        expect(component.mfaEnrollStep()).toBe('codes');
        expect(component.mfaRecoveryCodes()).toEqual(['AAAA-BBBB', 'CCCC-DDDD']);
        expect(component.mfaConfirming()).toBe(false);
      });

      it('service error sets mfaError', () => {
        mfaSvc.confirm.mockReturnValueOnce(
          throwError(() => ({ error: { message: 'invalid code' } })),
        );
        component.mfaConfirmCode.set('000000');
        component.confirmMfaEnrollment();
        expect(component.mfaError()).toBe('invalid code');
        expect(component.mfaConfirming()).toBe(false);
      });

      it('uses default error when backend gives none', () => {
        mfaSvc.confirm.mockReturnValueOnce(throwError(() => ({})));
        component.mfaConfirmCode.set('000000');
        component.confirmMfaEnrollment();
        expect(component.mfaError()).toBe('Invalid code. Try again.');
      });
    });

    describe('downloadRecoveryCodes()', () => {
      it('builds blob and triggers anchor click', () => {
        const click = vi.fn();
        const createObjectUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:fake');
        const revokeObjectUrl = vi.spyOn(URL, 'revokeObjectURL').mockReturnValue();
        const origCreate = document.createElement.bind(document);
        const createSpy = vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
          if (tag === 'a') {
            const a = origCreate('a') as HTMLAnchorElement;
            a.click = click;
            return a;
          }
          return origCreate(tag);
        });

        component.mfaRecoveryCodes.set(['AAAA-BBBB', 'CCCC-DDDD']);
        component.downloadRecoveryCodes();

        expect(createObjectUrl).toHaveBeenCalled();
        expect(click).toHaveBeenCalled();
        expect(revokeObjectUrl).toHaveBeenCalledWith('blob:fake');

        createSpy.mockRestore();
        createObjectUrl.mockRestore();
        revokeObjectUrl.mockRestore();
      });
    });

    describe('finishMfaEnrollment() / cancelMfaEnrollment()', () => {
      it('finishMfaEnrollment resets state + reloads status', () => {
        component.mfaEnrollStep.set('codes');
        component.mfaQrDataUrl.set('qr');
        component.mfaSecretBase32.set('s');
        component.mfaRecoveryCodes.set(['x']);
        component.mfaConfirmCode.set('123');
        mfaSvc.getStatus.mockClear();
        component.finishMfaEnrollment();
        expect(component.mfaEnrollStep()).toBe('idle');
        expect(component.mfaQrDataUrl()).toBeNull();
        expect(component.mfaSecretBase32()).toBeNull();
        expect(component.mfaRecoveryCodes()).toEqual([]);
        expect(component.mfaConfirmCode()).toBe('');
        expect(mfaSvc.getStatus).toHaveBeenCalledOnce();
      });

      it('cancelMfaEnrollment resets state without reload', () => {
        component.mfaEnrollStep.set('setup');
        component.mfaQrDataUrl.set('qr');
        component.mfaSecretBase32.set('s');
        component.mfaConfirmCode.set('123');
        component.mfaError.set('e');
        mfaSvc.getStatus.mockClear();
        component.cancelMfaEnrollment();
        expect(component.mfaEnrollStep()).toBe('idle');
        expect(component.mfaQrDataUrl()).toBeNull();
        expect(component.mfaSecretBase32()).toBeNull();
        expect(component.mfaConfirmCode()).toBe('');
        expect(component.mfaError()).toBeNull();
        expect(mfaSvc.getStatus).not.toHaveBeenCalled();
      });
    });

    describe('startMfaDisable() / cancelMfaDisable() / confirmMfaDisable()', () => {
      it('startMfaDisable transitions to confirm step + clears password', () => {
        component.mfaDisablePassword.set('stale');
        component.mfaError.set('old error');
        component.startMfaDisable();
        expect(component.mfaDisableStep()).toBe('confirm');
        expect(component.mfaDisablePassword()).toBe('');
        expect(component.mfaError()).toBeNull();
      });

      it('cancelMfaDisable resets disable state', () => {
        component.mfaDisableStep.set('confirm');
        component.mfaDisablePassword.set('hunter2');
        component.mfaError.set('e');
        component.cancelMfaDisable();
        expect(component.mfaDisableStep()).toBe('idle');
        expect(component.mfaDisablePassword()).toBe('');
        expect(component.mfaError()).toBeNull();
      });

      it('confirmMfaDisable empty password is a no-op', () => {
        component.mfaDisablePassword.set('');
        component.confirmMfaDisable();
        expect(mfaSvc.disable).not.toHaveBeenCalled();
      });

      it('confirmMfaDisable success resets state + reloads status', () => {
        component.mfaDisableStep.set('confirm');
        component.mfaDisablePassword.set('hunter2');
        mfaSvc.getStatus.mockClear();
        component.confirmMfaDisable();
        expect(mfaSvc.disable).toHaveBeenCalledWith('hunter2');
        expect(component.mfaDisableStep()).toBe('idle');
        expect(component.mfaDisablePassword()).toBe('');
        expect(component.mfaDisabling()).toBe(false);
        expect(mfaSvc.getStatus).toHaveBeenCalledOnce();
      });

      it('confirmMfaDisable service error sets mfaError', () => {
        mfaSvc.disable.mockReturnValueOnce(
          throwError(() => ({ error: { message: 'wrong password' } })),
        );
        component.mfaDisablePassword.set('bad');
        component.confirmMfaDisable();
        expect(component.mfaError()).toBe('wrong password');
        expect(component.mfaDisabling()).toBe(false);
      });

      it('confirmMfaDisable uses default error when backend gives none', () => {
        mfaSvc.disable.mockReturnValueOnce(throwError(() => ({})));
        component.mfaDisablePassword.set('bad');
        component.confirmMfaDisable();
        expect(component.mfaError()).toBe('Could not disable MFA.');
      });
    });
  });

  describe('API tokens section', () => {
    it('loadApiTokens populates list on init', () => {
      expect(apiTokensSvc.list).toHaveBeenCalled();
      expect(component.apiTokens()).toEqual([]);
    });

    it('loadApiTokens error falls back to empty array', async () => {
      apiTokensSvc.list.mockReturnValueOnce(throwError(() => new Error('boom')));
      TestBed.resetTestingModule();
      await build();
      expect(component.apiTokens()).toEqual([]);
    });

    describe('createApiToken()', () => {
      it('empty name is a no-op', () => {
        component.newTokenName.set('   ');
        component.createApiToken();
        expect(apiTokensSvc.create).not.toHaveBeenCalled();
      });

      it('success populates newTokenResult and reloads list', () => {
        component.newTokenName.set('ci-bot');
        component.createApiToken();
        expect(apiTokensSvc.create).toHaveBeenCalledWith({ name: 'ci-bot', expiresAt: null });
        expect(component.newTokenResult()?.plaintextToken).toBe('sop_lookup_secret');
        expect(component.newTokenName()).toBe('');
      });

      it('passes expiresAt when set', () => {
        component.newTokenName.set('ci-bot');
        component.newTokenExpiresAt.set('2027-01-01');
        component.createApiToken();
        expect(apiTokensSvc.create).toHaveBeenCalledWith({
          name: 'ci-bot',
          expiresAt: '2027-01-01',
        });
      });

      it('service error sets apiTokensError', () => {
        apiTokensSvc.create.mockReturnValueOnce(
          throwError(() => ({ error: { message: 'name taken' } })),
        );
        component.newTokenName.set('dup');
        component.createApiToken();
        expect(component.apiTokensError()).toBe('name taken');
        expect(component.creatingToken()).toBe(false);
      });

      it('default error when backend gives none', () => {
        apiTokensSvc.create.mockReturnValueOnce(throwError(() => ({})));
        component.newTokenName.set('x');
        component.createApiToken();
        expect(component.apiTokensError()).toBe('Could not create token.');
      });
    });

    describe('revokeApiToken()', () => {
      it('success clears revoking id and reloads list', () => {
        apiTokensSvc.list.mockClear();
        component.revokeApiToken('abc');
        expect(apiTokensSvc.revoke).toHaveBeenCalledWith('abc');
        expect(component.revokingTokenId()).toBeNull();
        expect(apiTokensSvc.list).toHaveBeenCalled();
      });

      it('service error sets apiTokensError', () => {
        apiTokensSvc.revoke.mockReturnValueOnce(
          throwError(() => ({ error: { message: 'forbidden' } })),
        );
        component.revokeApiToken('abc');
        expect(component.apiTokensError()).toBe('forbidden');
        expect(component.revokingTokenId()).toBeNull();
      });

      it('default error when backend gives none', () => {
        apiTokensSvc.revoke.mockReturnValueOnce(throwError(() => ({})));
        component.revokeApiToken('abc');
        expect(component.apiTokensError()).toBe('Could not revoke token.');
      });
    });

    describe('dismissNewToken() / copyNewToken()', () => {
      it('dismissNewToken clears the result', () => {
        component.newTokenName.set('ci-bot');
        component.createApiToken();
        expect(component.newTokenResult()).not.toBeNull();
        component.dismissNewToken();
        expect(component.newTokenResult()).toBeNull();
      });

      it('copyNewToken writes plaintext to clipboard', async () => {
        const writeText = vi.fn(() => Promise.resolve());
        Object.defineProperty(navigator, 'clipboard', {
          configurable: true,
          value: { writeText },
        });
        component.newTokenName.set('ci-bot');
        component.createApiToken();
        await component.copyNewToken();
        expect(writeText).toHaveBeenCalledWith('sop_lookup_secret');
      });

      it('copyNewToken swallows clipboard errors', async () => {
        const writeText = vi.fn(() => Promise.reject(new Error('blocked')));
        Object.defineProperty(navigator, 'clipboard', {
          configurable: true,
          value: { writeText },
        });
        component.newTokenName.set('ci-bot');
        component.createApiToken();
        await expect(component.copyNewToken()).resolves.toBeUndefined();
      });

      it('copyNewToken with no result is a no-op', async () => {
        const writeText = vi.fn();
        Object.defineProperty(navigator, 'clipboard', {
          configurable: true,
          value: { writeText },
        });
        await component.copyNewToken();
        expect(writeText).not.toHaveBeenCalled();
      });
    });
  });
});
