import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { GroupsService, UserGroup, UpdateGroupPayload } from './groups.service';

const mockGroup: UserGroup = {
  id: 'g1',
  name: 'Admins',
  description: 'Admin group',
  ownerUserId: 'u1',
  ownerName: 'Alice',
  createdAt: '2024-01-01T00:00:00Z',
  memberCount: 2,
  vaultCount: 3,
  members: [
    { id: 'u1', name: 'Alice', email: 'alice@example.com', isActive: true },
    { id: 'u2', name: 'Bob', email: 'bob@example.com', isActive: true },
  ],
};

describe('GroupsService', () => {
  let service: GroupsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [GroupsService, provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    service = TestBed.inject(GroupsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create with empty list', () => {
    expect(service).toBeTruthy();
    expect(service.list()).toEqual([]);
  });

  describe('reload()', () => {
    it('should GET /api/groups and update list signal', () => {
      service.reload().subscribe();
      const req = http.expectOne('/api/groups');
      expect(req.request.method).toBe('GET');
      req.flush([mockGroup]);

      expect(service.list().length).toBe(1);
      expect(service.list()[0].id).toBe('g1');
      expect(service.list()[0].name).toBe('Admins');
      expect(service.list()[0].memberCount).toBe(2);
    });

    it('should set empty list on empty response', () => {
      service.reload().subscribe();
      http.expectOne('/api/groups').flush([]);
      expect(service.list()).toEqual([]);
    });

    it('should populate members array', () => {
      service.reload().subscribe();
      http.expectOne('/api/groups').flush([mockGroup]);
      expect(service.list()[0].members.length).toBe(2);
      expect(service.list()[0].members[0].email).toBe('alice@example.com');
    });
  });

  describe('create()', () => {
    it('should POST /api/groups', () => {
      const payload = { name: 'Devs', description: 'Dev team', ownerUserId: null };
      let result: UserGroup | undefined;
      service.create(payload).subscribe((g) => (result = g));

      const req = http.expectOne('/api/groups');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(payload);
      req.flush({ ...mockGroup, id: 'g2', name: 'Devs' });
      expect(result?.id).toBe('g2');
    });
  });

  describe('rename()', () => {
    it('should PUT /api/groups/:id with name only', () => {
      service.rename('g1', 'New Name').subscribe();
      const req = http.expectOne('/api/groups/g1');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ name: 'New Name' });
      req.flush(mockGroup);
    });
  });

  describe('update()', () => {
    it('should PUT /api/groups/:id with full payload', () => {
      const payload: UpdateGroupPayload = {
        name: 'Updated',
        description: 'Updated desc',
        ownerUserId: 'u2',
      };
      service.update('g1', payload).subscribe();
      const req = http.expectOne('/api/groups/g1');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(payload);
      req.flush(mockGroup);
    });
  });

  describe('remove()', () => {
    it('should DELETE /api/groups/:id', () => {
      service.remove('g1').subscribe();
      const req = http.expectOne('/api/groups/g1');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });
  });

  describe('setMembers()', () => {
    it('should PUT /api/groups/:id/members with userIds', () => {
      service.setMembers('g1', ['u1', 'u2', 'u3']).subscribe();
      const req = http.expectOne('/api/groups/g1/members');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ userIds: ['u1', 'u2', 'u3'] });
      req.flush(null);
    });

    it('should PUT with empty array to clear members', () => {
      service.setMembers('g1', []).subscribe();
      const req = http.expectOne('/api/groups/g1/members');
      expect(req.request.body).toEqual({ userIds: [] });
      req.flush(null);
    });
  });
});
