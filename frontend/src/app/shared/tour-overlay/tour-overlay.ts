import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Mascot } from '../mascot/mascot';
import { OnboardingTourService, ONBOARDING_STEPS } from '../../services/onboarding-tour.service';
import { fadeIn, scaleIn } from '../animations';

@Component({
  selector: 'app-tour-overlay',
  standalone: true,
  imports: [RouterLink, TranslatePipe, Mascot],
  templateUrl: './tour-overlay.html',
  animations: [fadeIn, scaleIn],
})
export class TourOverlay {
  private readonly tour = inject(OnboardingTourService);

  readonly step = this.tour.activeStep;
  readonly stepNumber = computed(() => this.tour.currentStepIndex() + 1);
  readonly totalSteps = ONBOARDING_STEPS.length;
  readonly isFirst = computed(() => this.tour.currentStepIndex() === 0);
  readonly isLast = computed(() => this.tour.currentStepIndex() === ONBOARDING_STEPS.length - 1);
  readonly dots = computed(() => ONBOARDING_STEPS.map((_, i) => i <= this.tour.currentStepIndex()));

  back(): void {
    this.tour.back();
  }

  next(): void {
    this.tour.next();
  }

  skip(): void {
    this.tour.skip();
  }
}
