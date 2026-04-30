import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, expect, it } from 'vitest';
import { SsoFinalize } from './sso-finalize';

describe('SsoFinalize', () => {
  it('should create', () => {
    TestBed.configureTestingModule({
      imports: [SsoFinalize],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(SsoFinalize);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
