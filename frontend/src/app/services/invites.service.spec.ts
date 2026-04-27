import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { InvitesService } from './invites.service';

describe('InvitesService', () => {
  let service: InvitesService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(InvitesService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  describe('redeem', () => {
    it('should send a POST request with the correct URL and payload', () => {
      const token = 'test-token';
      const payload = { password: 'password123', name: 'Test User' };

      service.redeem(token, payload).subscribe();

      const req = httpTestingController.expectOne(`/api/invites/${token}/redeem`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(payload);
      req.flush(null);
    });

    it('should correctly encode the token in the URL', () => {
      const token = 'test token with / slashes';
      const encodedToken = encodeURIComponent(token);
      const payload = { password: 'password123' };

      service.redeem(token, payload).subscribe();

      const req = httpTestingController.expectOne(`/api/invites/${encodedToken}/redeem`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(payload);
      req.flush(null);
    });

    it('should propagate a 400 Bad Request error', () => {
      const token = 'invalid-token';
      const payload = { password: 'password123' };

      service.redeem(token, payload).subscribe({
        next: () => expect.fail('Should have failed with 400'),
        error: (error) => {
          expect(error.status).toBe(400);
          expect(error.statusText).toBe('Bad Request');
        },
      });

      const req = httpTestingController.expectOne(`/api/invites/${token}/redeem`);
      req.flush('Invalid token', { status: 400, statusText: 'Bad Request' });
    });

    it('should propagate a 404 Not Found error', () => {
      const token = 'nonexistent-token';
      const payload = { password: 'password123' };

      service.redeem(token, payload).subscribe({
        next: () => expect.fail('Should have failed with 404'),
        error: (error) => {
          expect(error.status).toBe(404);
          expect(error.statusText).toBe('Not Found');
        },
      });

      const req = httpTestingController.expectOne(`/api/invites/${token}/redeem`);
      req.flush('Token not found', { status: 404, statusText: 'Not Found' });
    });

    it('should propagate a 500 Internal Server Error', () => {
      const token = 'valid-token';
      const payload = { password: 'password123' };

      service.redeem(token, payload).subscribe({
        next: () => expect.fail('Should have failed with 500'),
        error: (error) => {
          expect(error.status).toBe(500);
          expect(error.statusText).toBe('Internal Server Error');
        },
      });

      const req = httpTestingController.expectOne(`/api/invites/${token}/redeem`);
      req.flush('Server error', { status: 500, statusText: 'Internal Server Error' });
    });
  });
});
