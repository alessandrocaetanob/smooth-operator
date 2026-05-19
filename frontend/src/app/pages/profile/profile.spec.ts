import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { Profile } from './profile';
import { ProfileService } from '../../services/profile.service';
import { AuthService } from '../../services/auth.service';

describe('Profile', () => {
  let fixture: ComponentFixture<Profile>;
  let component: Profile;
  let profileSvc: {
    updateProfile: ReturnType<typeof vi.fn>;
    removeAvatar: ReturnType<typeof vi.fn>;
  };
  let auth: {
    currentUser: ReturnType<
      typeof signal<{ id: string; name: string; avatarUrl?: string | null } | null>
    >;
  };

  beforeEach(async () => {
    profileSvc = {
      updateProfile: vi.fn(() => of(undefined)),
      removeAvatar: vi.fn(() => of(undefined)),
    };
    auth = {
      currentUser: signal<{ id: string; name: string; avatarUrl?: string | null } | null>({
        id: 'u',
        name: 'Alice Wonder',
        avatarUrl: 'https://x/a.png',
      }),
    };

    await TestBed.configureTestingModule({
      imports: [Profile],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ProfileService, useValue: profileSvc },
        { provide: AuthService, useValue: auth },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Profile);
    component = fixture.componentInstance;
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
});
