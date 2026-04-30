import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { ForgotPassword } from './forgot-password';

describe('ForgotPassword', () => {
  let component: ForgotPassword;
  let fixture: ComponentFixture<ForgotPassword>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ForgotPassword],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(ForgotPassword);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);

    fixture.detectChanges();
    // Flush the smtp-available probe on init
    httpMock.expectOne('/api/auth/smtp-available').flush({ available: true });
    await fixture.whenStable();
  });

  afterEach(() => httpMock.verify());

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should show success state after submit', async () => {
    component.form.setValue({ email: 'user@example.com' });
    component.submit();

    httpMock.expectOne('/api/auth/forgot-password').flush({}, { status: 200, statusText: 'OK' });
    await fixture.whenStable();

    expect(component.submitted()).toBe(true);
    expect(component.submitting()).toBe(false);
    expect(component.errorMessage()).toBeNull();
  });

  it('should show error message on HTTP transport failure', async () => {
    component.form.setValue({ email: 'user@example.com' });
    component.submit();

    httpMock
      .expectOne('/api/auth/forgot-password')
      .flush('Server error', { status: 500, statusText: 'Internal Server Error' });
    await fixture.whenStable();

    expect(component.submitted()).toBe(false);
    expect(component.submitting()).toBe(false);
    expect(component.errorMessage()).toBeTruthy();
  });
});
