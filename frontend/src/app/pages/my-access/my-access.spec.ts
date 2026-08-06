import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { MyAccess } from './my-access';
import { UsersService, EffectiveVaults } from '../../services/users.service';
import { AuthService } from '../../services/auth.service';

describe('MyAccess', () => {
  let component: MyAccess;
  let fixture: ComponentFixture<MyAccess>;
  let usersSvc: { getEffectiveVaults: ReturnType<typeof vi.fn> };
  let auth: { currentUser: ReturnType<typeof signal<{ id: string } | null>> };

  const sample: EffectiveVaults = {
    direct: [{ id: 'v1', name: 'a' }],
    viaGroups: [{ groupId: 'g1', groupName: 'team', vaults: [{ id: 'v2', name: 'b' }] }],
    merged: [
      { id: 'v1', name: 'a' },
      { id: 'v2', name: 'b' },
    ],
  } as unknown as EffectiveVaults;

  beforeEach(async () => {
    usersSvc = { getEffectiveVaults: vi.fn(() => of(sample)) };
    auth = { currentUser: signal<{ id: string } | null>({ id: 'u1' }) };

    await TestBed.configureTestingModule({
      imports: [MyAccess],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: UsersService, useValue: usersSvc },
        { provide: AuthService, useValue: auth },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      pages: {
        myAccess: {
          notSignedIn: 'You are not signed in.',
          loadFailed: 'Failed to load access.',
        },
      },
    });
    translate.use('en');

    fixture = TestBed.createComponent(MyAccess);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('ngOnInit loads effective vaults and updates counts', () => {
    component.ngOnInit();
    expect(component.effective()).toBe(sample);
    expect(component.mergedCount()).toBe(2);
    expect(component.directCount()).toBe(1);
    expect(component.groupCount()).toBe(1);
    expect(component.loading()).toBe(false);
  });

  it('shows error when no user', () => {
    auth.currentUser.set(null);
    component.ngOnInit();
    expect(component.errorMessage()).toBe('You are not signed in.');
    expect(component.loading()).toBe(false);
  });

  it('surfaces backend error', () => {
    usersSvc.getEffectiveVaults.mockReturnValueOnce(throwError(() => ({ message: 'bad' })));
    component.ngOnInit();
    expect(component.errorMessage()).toBe('bad');
  });

  it('falls back to default error message', () => {
    usersSvc.getEffectiveVaults.mockReturnValueOnce(throwError(() => ({})));
    component.ngOnInit();
    expect(component.errorMessage()).toBe('Failed to load access.');
  });

  it('trackBy helpers return id / groupId', () => {
    expect(component.trackById(0, { id: 'v1' })).toBe('v1');
    expect(component.trackGroup(0, { groupId: 'g1' })).toBe('g1');
  });

  it('counts default to 0 with no effective data', () => {
    expect(component.mergedCount()).toBe(0);
    expect(component.directCount()).toBe(0);
    expect(component.groupCount()).toBe(0);
  });
});
