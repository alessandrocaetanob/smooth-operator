import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import { ProfileService, UpdateProfilePayload } from './profile.service';
import { AuthService, UserInfo } from './auth.service';

const mockUserInfo: UserInfo = {
  id: 'u1',
  email: 'alice@example.com',
  name: 'Alice',
  hasPassword: true,
  ssoLinked: false,
  ssoProviderType: null,
  avatarUrl: null,
  roles: ['admin'],
};

describe('ProfileService', () => {
  let service: ProfileService;
  let http: HttpTestingController;
  let mockAuthService: { setCurrentUser: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    mockAuthService = { setCurrentUser: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        ProfileService,
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: mockAuthService },
      ],
    });
    service = TestBed.inject(ProfileService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create', () => {
    expect(service).toBeTruthy();
  });

  describe('updateProfile()', () => {
    it('should PUT /api/users/me/profile with name', () => {
      const payload: UpdateProfilePayload = { name: 'New Name' };
      let result: UserInfo | undefined;
      service.updateProfile(payload).subscribe((u) => (result = u));

      const req = http.expectOne('/api/users/me/profile');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ Name: 'New Name' });
      req.flush({ id: 'u1', name: 'New Name', email: 'alice@example.com' });
      expect(result?.name).toBe('New Name');
      expect(mockAuthService.setCurrentUser).toHaveBeenCalledOnce();
    });

    it('should include avatar fields when avatarBase64 is set', () => {
      const payload: UpdateProfilePayload = {
        name: 'Alice',
        avatarBase64: 'data:image/png;base64,abc123',
        avatarMimeType: 'image/png',
      };
      service.updateProfile(payload).subscribe();

      const req = http.expectOne('/api/users/me/profile');
      expect(req.request.body).toMatchObject({
        Name: 'Alice',
        AvatarBase64: 'data:image/png;base64,abc123',
        AvatarMimeType: 'image/png',
      });
      req.flush(mockUserInfo);
    });

    it('should NOT include avatar fields when avatarBase64 is absent', () => {
      service.updateProfile({ name: 'Alice' }).subscribe();
      const req = http.expectOne('/api/users/me/profile');
      expect(req.request.body).not.toHaveProperty('AvatarBase64');
      req.flush(mockUserInfo);
    });

    it('should call auth.setCurrentUser with normalized UserInfo', () => {
      service.updateProfile({ name: 'Alice' }).subscribe();
      http.expectOne('/api/users/me/profile').flush({
        id: 'u1',
        email: 'alice@example.com',
        name: 'Alice',
        hasPassword: true,
        ssoLinked: false,
        ssoProviderType: null,
        avatarUrl: '/avatars/u1.png',
        roles: ['admin'],
      });

      expect(mockAuthService.setCurrentUser).toHaveBeenCalledWith(
        expect.objectContaining({ id: 'u1', name: 'Alice', avatarUrl: '/avatars/u1.png' }),
      );
    });
  });

  describe('removeAvatar()', () => {
    it('should DELETE /api/users/me/avatar', () => {
      let result: UserInfo | undefined;
      service.removeAvatar().subscribe((u) => (result = u));

      const req = http.expectOne('/api/users/me/avatar');
      expect(req.request.method).toBe('DELETE');
      req.flush({ ...mockUserInfo, avatarUrl: null });
      expect(result?.avatarUrl).toBeNull();
      expect(mockAuthService.setCurrentUser).toHaveBeenCalledOnce();
    });

    it('should call auth.setCurrentUser after removing avatar', () => {
      service.removeAvatar().subscribe();
      http.expectOne('/api/users/me/avatar').flush(mockUserInfo);
      expect(mockAuthService.setCurrentUser).toHaveBeenCalledOnce();
    });
  });
});
