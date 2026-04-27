import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { AuthService, UserInfo } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService],
    });

    service = TestBed.inject(AuthService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    if (httpTestingController) {
      httpTestingController.verify();
    }
  });

  describe('me()', () => {
    it('should fetch user info and update currentUser state', () => {
      const mockUserInfo: UserInfo = {
        id: '123',
        email: 'test@example.com',
        name: 'Test User',
        hasPassword: true,
        linkedToEntra: false,
        roles: ['Admin'],
      };

      let emittedUser: UserInfo | undefined;
      service.me().subscribe((user) => {
        emittedUser = user;
      });

      const req = httpTestingController.expectOne('/api/auth/me');
      expect(req.request.method).toEqual('GET');
      req.flush(mockUserInfo);

      expect(emittedUser).toBeTruthy();
      expect(emittedUser?.id).toBe('123');
      expect(emittedUser?.email).toBe('test@example.com');

      // Verify that the _user signal (exposed via currentUser) was updated
      expect(service.currentUser()?.id).toBe('123');
      expect(service.currentUser()?.email).toBe('test@example.com');
    });

    it('should throw an error when the server returns an error', () => {
      let errorThrown = false;
      service.me().subscribe({
        next: () => {},
        error: () => {
          errorThrown = true;
        },
      });

      const req = httpTestingController.expectOne('/api/auth/me');
      req.flush('Error', { status: 500, statusText: 'Internal Server Error' });

      expect(errorThrown).toBe(true);
    });
  });
});
