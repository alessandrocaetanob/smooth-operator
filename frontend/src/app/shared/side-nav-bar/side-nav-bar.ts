import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-side-nav-bar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './side-nav-bar.html',
  styleUrl: './side-nav-bar.css',
})
export class SideNavBar {
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  readonly canManageConnections = this.auth.canManageConnections;
  readonly canViewCredentials = this.auth.canViewCredentials;
  readonly canAccessSettings = this.auth.canAccessSettings;

  newConnection(): void {
    if (!this.canManageConnections()) {
      this.router.navigate(['/vault']);
      return;
    }
    this.router.navigate(['/connections']);
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
