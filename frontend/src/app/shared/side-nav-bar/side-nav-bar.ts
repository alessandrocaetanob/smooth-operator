import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { filter, map } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { LayoutService } from '../../services/layout.service';
import { RuntimeConfigService } from '../../core/config/runtime-config.service';

@Component({
  selector: 'app-side-nav-bar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './side-nav-bar.html',
  styleUrl: './side-nav-bar.css',
})
export class SideNavBar {
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly runtimeConfig = inject(RuntimeConfigService);
  readonly layout = inject(LayoutService);

  readonly canManageConnections = this.auth.canManageConnections;
  readonly canViewCredentials = this.auth.canViewCredentials;
  readonly canAccessSettings = this.auth.canAccessSettings;
  readonly collapsed = this.layout.collapsed;
  readonly mobileNavOpen = this.layout.mobileNavOpen;

  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );
  readonly isSettings = computed(() => this.currentUrl().startsWith('/settings'));

  get helpUrl(): string {
    return this.runtimeConfig.config.helpUrl;
  }

  toggleCollapsed(): void {
    this.layout.toggle();
  }

  closeMobileNav(): void {
    this.layout.closeMobileNav();
  }

  newConnection(): void {
    if (!this.canManageConnections()) {
      this.router.navigate(['/vault']);
      return;
    }
    this.router.navigate(['/connections']);
  }

  logout(): void {
    this.auth.logout();
    this.layout.closeMobileNav();
    this.router.navigate(['/login']);
  }
}
