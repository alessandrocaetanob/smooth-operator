import { Injectable, computed, inject, signal } from '@angular/core';
import { AuthService } from './auth.service';
import { MascotState } from '../shared/mascot/mascot';

export type OnboardingStepId = 'host' | 'credential' | 'connection' | 'session' | 'invite';
export type TourStatus = 'not-started' | 'active' | 'dismissed' | 'completed';

export interface OnboardingStepConfig {
  id: OnboardingStepId;
  route: string;
  mascotState: MascotState;
}

export const ONBOARDING_STEPS: OnboardingStepConfig[] = [
  { id: 'host', route: '/hosts', mascotState: 'wave' },
  { id: 'credential', route: '/credentials', mascotState: 'typing' },
  { id: 'connection', route: '/connections', mascotState: 'thinking' },
  { id: 'session', route: '/vault', mascotState: 'connected' },
  { id: 'invite', route: '/settings/users', mascotState: 'success' },
];

interface PersistedState {
  status: TourStatus;
  completedSteps: OnboardingStepId[];
  currentStepIndex: number;
}

const STORAGE_PREFIX = 'onboarding-tour:';

@Injectable({ providedIn: 'root' })
export class OnboardingTourService {
  private readonly auth = inject(AuthService);

  private readonly _status = signal<TourStatus>('not-started');
  private readonly _completedSteps = signal<Set<OnboardingStepId>>(new Set());
  private readonly _currentStepIndex = signal(0);
  private initialized = false;

  readonly status = this._status.asReadonly();
  readonly completedSteps = this._completedSteps.asReadonly();
  readonly currentStepIndex = this._currentStepIndex.asReadonly();

  readonly activeStep = computed<OnboardingStepConfig | null>(() =>
    this._status() === 'active' ? ONBOARDING_STEPS[this._currentStepIndex()] : null,
  );

  readonly progress = computed(() => ({
    done: this._completedSteps().size,
    total: ONBOARDING_STEPS.length,
  }));

  readonly showChecklist = computed(() => this.auth.isOwner() && this._status() !== 'completed');

  /** Call once, after the authenticated shell mounts. Idempotent. */
  init(): void {
    if (this.initialized) return;
    this.initialized = true;

    const userId = this.auth.currentUser()?.id;
    if (!userId) return;

    const saved = this.readStorage(userId);
    if (saved) {
      this._status.set(saved.status);
      this._completedSteps.set(new Set(saved.completedSteps));
      this._currentStepIndex.set(saved.currentStepIndex);
      return;
    }

    if (this.auth.isOwner()) {
      this._status.set('active');
      this._currentStepIndex.set(0);
      this.persist();
    }
  }

  next(): void {
    if (this._status() !== 'active') return;
    if (this._currentStepIndex() >= ONBOARDING_STEPS.length - 1) {
      this._status.set('completed');
    } else {
      this._currentStepIndex.update((i) => i + 1);
    }
    this.persist();
  }

  back(): void {
    if (this._status() !== 'active') return;
    this._currentStepIndex.update((i) => Math.max(0, i - 1));
    this.persist();
  }

  skip(): void {
    if (this._status() === 'completed') return;
    this._status.set('dismissed');
    this.persist();
  }

  resume(stepId?: OnboardingStepId): void {
    if (this._status() === 'completed') return;
    if (stepId) {
      const index = ONBOARDING_STEPS.findIndex((s) => s.id === stepId);
      if (index >= 0) this._currentStepIndex.set(index);
    }
    this._status.set('active');
    this.persist();
  }

  completeStep(id: OnboardingStepId): void {
    if (this._status() === 'completed') return;
    if (this._completedSteps().has(id)) return;

    this._completedSteps.update((set) => new Set(set).add(id));

    if (this._completedSteps().size >= ONBOARDING_STEPS.length) {
      this._status.set('completed');
    } else if (this._status() === 'active' && this.activeStep()?.id === id) {
      this.next();
      return;
    }
    this.persist();
  }

  private readStorage(userId: string): PersistedState | null {
    try {
      const raw = localStorage.getItem(STORAGE_PREFIX + userId);
      if (!raw) return null;
      const parsed = JSON.parse(raw) as Partial<PersistedState>;
      if (!parsed || typeof parsed !== 'object') return null;
      return {
        status: parsed.status ?? 'not-started',
        completedSteps: Array.isArray(parsed.completedSteps) ? parsed.completedSteps : [],
        currentStepIndex: typeof parsed.currentStepIndex === 'number' ? parsed.currentStepIndex : 0,
      };
    } catch {
      return null;
    }
  }

  private persist(): void {
    const userId = this.auth.currentUser()?.id;
    if (!userId) return;
    const state: PersistedState = {
      status: this._status(),
      completedSteps: Array.from(this._completedSteps()),
      currentStepIndex: this._currentStepIndex(),
    };
    try {
      localStorage.setItem(STORAGE_PREFIX + userId, JSON.stringify(state));
    } catch {
      // localStorage unavailable (private mode, quota) — onboarding state stays in-memory only.
    }
  }
}
