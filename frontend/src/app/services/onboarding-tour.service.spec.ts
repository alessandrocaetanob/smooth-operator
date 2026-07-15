import { TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach } from 'vitest';

import { AuthService, UserInfo } from './auth.service';
import { OnboardingTourService, ONBOARDING_STEPS } from './onboarding-tour.service';

function makeUser(overrides: Partial<UserInfo> = {}): UserInfo {
  return {
    id: 'user-1',
    email: 'owner@example.com',
    name: 'Owner',
    hasPassword: true,
    ssoLinked: false,
    ssoProviderType: null,
    roles: ['Owner'],
    ...overrides,
  };
}

describe('OnboardingTourService', () => {
  let service: OnboardingTourService;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    auth = TestBed.inject(AuthService);
    service = TestBed.inject(OnboardingTourService);
  });

  it('does not auto-start when no user is set', () => {
    service.init();
    expect(service.status()).toBe('not-started');
    expect(service.activeStep()).toBeNull();
  });

  it('auto-starts for an Owner with no saved state', () => {
    auth.setCurrentUser(makeUser());
    service.init();
    expect(service.status()).toBe('active');
    expect(service.activeStep()?.id).toBe(ONBOARDING_STEPS[0].id);
  });

  it('does not auto-start for a non-Owner', () => {
    auth.setCurrentUser(makeUser({ roles: ['Admin'] }));
    service.init();
    expect(service.status()).toBe('not-started');
    expect(service.activeStep()).toBeNull();
  });

  it('is idempotent — a second init() does not reset progress', () => {
    auth.setCurrentUser(makeUser());
    service.init();
    service.next();
    service.init();
    expect(service.currentStepIndex()).toBe(1);
  });

  it('restores persisted state for a returning user', () => {
    auth.setCurrentUser(makeUser());
    service.init();
    service.completeStep('host');
    service.skip();

    // Fresh service instance, same TestBed injector re-created
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const auth2 = TestBed.inject(AuthService);
    auth2.setCurrentUser(makeUser());
    const service2 = TestBed.inject(OnboardingTourService);
    service2.init();

    expect(service2.status()).toBe('dismissed');
    expect(service2.completedSteps().has('host')).toBe(true);
  });

  describe('next() / back()', () => {
    beforeEach(() => {
      auth.setCurrentUser(makeUser());
      service.init();
    });

    it('advances to the next step', () => {
      service.next();
      expect(service.currentStepIndex()).toBe(1);
      expect(service.status()).toBe('active');
    });

    it('completes the tour after the last step', () => {
      for (const _ of ONBOARDING_STEPS.slice(1)) service.next();
      expect(service.status()).toBe('active');
      service.next();
      expect(service.status()).toBe('completed');
      expect(service.activeStep()).toBeNull();
    });

    it('does not go before the first step', () => {
      service.back();
      expect(service.currentStepIndex()).toBe(0);
    });

    it('goes back a step', () => {
      service.next();
      service.back();
      expect(service.currentStepIndex()).toBe(0);
    });
  });

  describe('skip() / resume()', () => {
    beforeEach(() => {
      auth.setCurrentUser(makeUser());
      service.init();
    });

    it('skip() hides the overlay but keeps progress', () => {
      service.next();
      service.skip();
      expect(service.status()).toBe('dismissed');
      expect(service.activeStep()).toBeNull();
      expect(service.currentStepIndex()).toBe(1);
    });

    it('resume() re-activates at the saved step', () => {
      service.next();
      service.skip();
      service.resume();
      expect(service.status()).toBe('active');
      expect(service.activeStep()?.id).toBe(ONBOARDING_STEPS[1].id);
    });

    it('resume(stepId) jumps to a specific step', () => {
      service.skip();
      service.resume('connection');
      expect(service.activeStep()?.id).toBe('connection');
    });

    it('a completed tour cannot be resumed', () => {
      for (const step of ONBOARDING_STEPS) service.completeStep(step.id);
      expect(service.status()).toBe('completed');
      service.resume();
      expect(service.status()).toBe('completed');
    });
  });

  describe('completeStep()', () => {
    beforeEach(() => {
      auth.setCurrentUser(makeUser());
      service.init();
    });

    it('auto-advances when completing the active step', () => {
      service.completeStep('host');
      expect(service.completedSteps().has('host')).toBe(true);
      expect(service.currentStepIndex()).toBe(1);
      expect(service.status()).toBe('active');
    });

    it('records completion without advancing when the step is not the active one', () => {
      service.completeStep('invite');
      expect(service.completedSteps().has('invite')).toBe(true);
      expect(service.currentStepIndex()).toBe(0);
    });

    it('is idempotent for an already-completed step', () => {
      service.completeStep('host');
      service.completeStep('host');
      expect(service.completedSteps().size).toBe(1);
    });

    it('marks the tour completed once all steps are done', () => {
      for (const step of ONBOARDING_STEPS) service.completeStep(step.id);
      expect(service.status()).toBe('completed');
      expect(service.progress()).toEqual({
        done: ONBOARDING_STEPS.length,
        total: ONBOARDING_STEPS.length,
      });
    });

    it('still records organic completions after the tour was dismissed', () => {
      service.skip();
      service.completeStep('credential');
      expect(service.completedSteps().has('credential')).toBe(true);
      expect(service.status()).toBe('dismissed');
    });
  });

  describe('showChecklist()', () => {
    it('is true for an Owner while not completed', () => {
      auth.setCurrentUser(makeUser());
      service.init();
      expect(service.showChecklist()).toBe(true);
    });

    it('is false for a non-Owner', () => {
      auth.setCurrentUser(makeUser({ roles: ['Admin'] }));
      service.init();
      expect(service.showChecklist()).toBe(false);
    });

    it('is false once the tour is completed', () => {
      auth.setCurrentUser(makeUser());
      service.init();
      for (const step of ONBOARDING_STEPS) service.completeStep(step.id);
      expect(service.showChecklist()).toBe(false);
    });
  });
});
