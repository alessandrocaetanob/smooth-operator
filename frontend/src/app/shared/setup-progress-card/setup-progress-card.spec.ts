import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach } from 'vitest';

import { SetupProgressCard } from './setup-progress-card';
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

describe('SetupProgressCard', () => {
  let component: SetupProgressCard;
  let fixture: ComponentFixture<SetupProgressCard>;
  let tour: OnboardingTourService;
  let auth: AuthService;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [SetupProgressCard],
      providers: [provideTranslateService()],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      pages: {
        onboarding: {
          checklist: {
            title: 'Setup checklist',
            progress: '{{done}}/{{total}} done',
            hide: 'Hide',
          },
          steps: {
            host: { title: 'Add a host' },
            credential: { title: 'Add a credential' },
            connection: { title: 'Add a connection' },
            session: { title: 'Open a session' },
            invite: { title: 'Invite a teammate' },
          },
        },
      },
    });
    translate.use('en');

    auth = TestBed.inject(AuthService);
    tour = TestBed.inject(OnboardingTourService);
    fixture = TestBed.createComponent(SetupProgressCard);
    component = fixture.componentInstance;
  });

  it('is hidden for a non-Owner', () => {
    auth.setCurrentUser({ ...makeOwner(), roles: ['Admin'] });
    tour.init();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent.trim()).toBe('');
  });

  it('renders all steps for an Owner', () => {
    auth.setCurrentUser(makeOwner());
    tour.init();
    fixture.detectChanges();

    const items = fixture.nativeElement.querySelectorAll('li');
    expect(items.length).toBe(ONBOARDING_STEPS.length);
    expect(fixture.nativeElement.textContent).toContain('Setup checklist');
  });

  it('marks completed steps as done', () => {
    auth.setCurrentUser(makeOwner());
    tour.init();
    tour.completeStep('host');
    fixture.detectChanges();

    expect(component.isDone('host')).toBe(true);
    expect(component.isDone('credential')).toBe(false);
  });

  it('clicking a step resumes the tour at that step', () => {
    auth.setCurrentUser(makeOwner());
    tour.init();
    tour.skip();

    component.resume('connection');

    expect(tour.status()).toBe('active');
    expect(tour.activeStep()?.id).toBe('connection');
  });

  it('hide() dismisses the tour', () => {
    auth.setCurrentUser(makeOwner());
    tour.init();

    component.hide();

    expect(tour.status()).toBe('dismissed');
  });

  it('disappears once the tour is completed', () => {
    auth.setCurrentUser(makeOwner());
    tour.init();
    for (const step of ONBOARDING_STEPS) tour.completeStep(step.id);
    fixture.detectChanges();

    expect(component.visible()).toBe(false);
    expect(fixture.nativeElement.textContent.trim()).toBe('');
  });
});
