import { Component, inject, Input, Output, EventEmitter } from '@angular/core';
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

  @Input() mobileVisible = false;
  @Output() readonly closeNav = new EventEmitter<void>();

  get navClass(): string {
    const base =
      'flex-col fixed left-0 top-16 bottom-0 w-64 z-40 bg-slate-950/90 backdrop-blur-xl border-r border-white/10 shadow-2xl shadow-blue-900/20 font-inter text-xs font-medium uppercase tracking-widest antialiased';
    return `${base} ${this.mobileVisible ? 'flex' : 'hidden md:flex'}`;
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
    this.router.navigate(['/login']);
  }
}
