import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideAnimations } from '@angular/platform-browser/animations';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach } from 'vitest';

import { TourOverlay } from './tour-overlay';
import { OnboardingTourService, ONBOARDING_STEPS } from '../../services/onboarding-tour.service';
import { AuthService, UserInfo } from '../../services/auth.service';

function makeOwner(): UserInfo {
  return {
    id: 'owner-1',
    email: 'owner@example.com',
    name: 'Owner',
    hasPassword: true,
    ssoLinked: false,
    ssoProviderType: null,
    roles: ['Owner'],
  };
}

describe('TourOverlay', () => {
  let component: TourOverlay;
  let fixture: ComponentFixture<TourOverlay>;
  let tour: OnboardingTourService;
  let auth: AuthService;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [TourOverlay],
      providers: [provideRouter([]), provideAnimations(), provideTranslateService()],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      pages: {
        onboarding: {
          stepLabel: 'Step {{current}} of {{total}}',
          back: 'Back',
          next: 'Next',
          finish: 'Finish',
          skip: 'Skip',
          ariaLabel: 'Onboarding tour',
          steps: {
            host: { title: 'Add a host', body: 'Register a machine.', cta: 'Go to Hosts' },
            credential: {
              title: 'Add a credential',
              body: 'Store a secret.',
              cta: 'Go to Credentials',
            },
            connection: {
              title: 'Add a connection',
              body: 'Link host + credential.',
              cta: 'Go to Connections',
            },
            session: {
              title: 'Open a session',
              body: 'Launch from the vault.',
              cta: 'Go to Vault',
            },
            invite: { title: 'Invite a teammate', body: 'Share access.', cta: 'Go to Users' },
          },
        },
      },
    });
    translate.use('en');

    auth = TestBed.inject(AuthService);
    tour = TestBed.inject(OnboardingTourService);
    fixture = TestBed.createComponent(TourOverlay);
    component = fixture.componentInstance;
  });

  it('renders nothing when there is no active step', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="region"]')).toBeNull();
  });

  it('renders the active step once the tour starts', () => {
    auth.setCurrentUser(makeOwner());
    tour.init();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="region"]')).not.toBeNull();
    expect(el.textContent).toContain('Add a host');
    expect(component.stepNumber()).toBe(1);
    expect(component.isFirst()).toBe(true);
    expect(component.isLast()).toBe(false);
  });

  it('next() advances the underlying service', () => {
    auth.setCurrentUser(makeOwner());
    tour.init();
    fixture.detectChanges();

    component.next();
    fixture.detectChanges();

    expect(tour.currentStepIndex()).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Add a credential');
  });

  it('back() is a no-op on the first step', () => {
    auth.setCurrentUser(makeOwner());
    tour.init();
    component.back();
    expect(tour.currentStepIndex()).toBe(0);
  });

  it('skip() hides the overlay', () => {
    auth.setCurrentUser(makeOwner());
    tour.init();
    fixture.detectChanges();

    component.skip();

    expect(tour.status()).toBe('dismissed');
    expect(component.step()).toBeNull();
  });

  it('shows the finish label on the last step', () => {
    auth.setCurrentUser(makeOwner());
    tour.init();
    for (let i = 0; i < ONBOARDING_STEPS.length - 1; i++) tour.next();
    fixture.detectChanges();

    expect(component.isLast()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Finish');
  });
});
