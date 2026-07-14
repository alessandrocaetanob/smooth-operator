import { TestBed } from '@angular/core/testing';
import { describe, it, expect } from 'vitest';
import { provideTranslateService } from '@ngx-translate/core';
import { SettingsRoles } from './roles';

describe('SettingsRoles', () => {
  it('should create with role descriptors', () => {
    TestBed.configureTestingModule({
      imports: [SettingsRoles],
      providers: [provideTranslateService()],
    });
    const fixture = TestBed.createComponent(SettingsRoles);
    expect(fixture.componentInstance.roles.length).toBeGreaterThan(0);
  });

  it('trackRole returns key', () => {
    TestBed.configureTestingModule({
      imports: [SettingsRoles],
      providers: [provideTranslateService()],
    });
    const fixture = TestBed.createComponent(SettingsRoles);
    const role = fixture.componentInstance.roles[0];
    expect(fixture.componentInstance.trackRole(0, role)).toBe(role.key);
  });
});
