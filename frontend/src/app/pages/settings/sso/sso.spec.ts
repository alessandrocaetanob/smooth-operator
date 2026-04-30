import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, expect, it } from 'vitest';
import { SsoSettings } from './sso';

describe('SsoSettings', () => {
  it('should create', () => {
    TestBed.configureTestingModule({
      imports: [SsoSettings],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(SsoSettings);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
