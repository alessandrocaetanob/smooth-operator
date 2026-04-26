import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Mascot } from '../mascot/mascot';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-top-nav-bar',
  standalone: true,
  imports: [RouterLink, Mascot],
  templateUrl: './top-nav-bar.html',
  styleUrl: './top-nav-bar.css',
})
export class TopNavBar {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly user = this.auth.currentUser;
  readonly canAccessSettings = this.auth.canAccessSettings;
  readonly initials = computed(() => {
    const u = this.user();
    if (!u?.name) return '?';
    const parts = u.name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  });

  menuOpen = false;

  toggleMenu(): void {
    this.menuOpen = !this.menuOpen;
  }

  closeMenu(): void {
    this.menuOpen = false;
  }

  logout(): void {
    this.auth.logout();
    this.menuOpen = false;
    this.router.navigate(['/login']);
  }
}
