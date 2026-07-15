import { Component, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import {
  OnboardingTourService,
  ONBOARDING_STEPS,
  OnboardingStepId,
} from '../../services/onboarding-tour.service';

@Component({
  selector: 'app-setup-progress-card',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './setup-progress-card.html',
})
export class SetupProgressCard {
  private readonly tour = inject(OnboardingTourService);

  readonly steps = ONBOARDING_STEPS;
  readonly visible = this.tour.showChecklist;
  readonly progress = this.tour.progress;
  readonly completedSteps = this.tour.completedSteps;

  isDone(id: OnboardingStepId): boolean {
    return this.completedSteps().has(id);
  }

  resume(id: OnboardingStepId): void {
    this.tour.resume(id);
  }

  hide(): void {
    this.tour.skip();
  }
}
