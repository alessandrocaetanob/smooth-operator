import { Component, DOCUMENT, effect, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TopNavBar } from '../top-nav-bar/top-nav-bar';
import { SideNavBar } from '../side-nav-bar/side-nav-bar';
import { ConfirmDialog } from '../confirm-dialog/confirm-dialog';
import { ToastContainer } from '../toast/toast';
import { TourOverlay } from '../tour-overlay/tour-overlay';
import { LayoutService } from '../../services/layout.service';
import { OnboardingTourService } from '../../services/onboarding-tour.service';
import { routeFade } from '../animations';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, TopNavBar, SideNavBar, ConfirmDialog, ToastContainer, TourOverlay],
  templateUrl: './layout.html',
  styleUrl: './layout.css',
  animations: [routeFade],
})
export class Layout {
  readonly layout = inject(LayoutService);
  readonly collapsed = this.layout.collapsed;
  private readonly document = inject(DOCUMENT);
  private readonly tour = inject(OnboardingTourService);

  constructor() {
    effect(() => {
      this.document.documentElement.classList.toggle('sidebar-collapsed', this.collapsed());
    });
    this.tour.init();
  }
}
