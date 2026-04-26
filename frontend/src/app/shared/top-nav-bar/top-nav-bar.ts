import { Component, DestroyRef, NgZone, OnInit, computed, inject, signal } from '@angular/core';

import { Router, RouterLink } from '@angular/router';
import { Mascot, MascotState } from '../mascot/mascot';
import { ThemeToggle } from '../theme-toggle/theme-toggle';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { ToastService } from '../toast/toast.service';

const IDLE_TIMEOUT_MS = 3 * 60 * 1000;

@Component({
  selector: 'app-top-nav-bar',
  standalone: true,
  imports: [RouterLink, Mascot, ThemeToggle],
  templateUrl: './top-nav-bar.html',
  styleUrl: './top-nav-bar.css',
})
export class TopNavBar implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);
  private readonly destroyRef = inject(DestroyRef);
  readonly themeService = inject(ThemeService);
  private readonly toastService = inject(ToastService);

  private readonly _idleState = signal<'idle' | 'sleep'>('idle');
  private idleTimer: ReturnType<typeof setTimeout> | null = null;

  readonly navMascotState = computed<MascotState>(() =>
    this.toastService.toasts().some((t) => t.kind === 'error') ? 'error' : this._idleState()
  );

  readonly scrolled = signal(false);

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

  ngOnInit(): void {
    this.zone.runOutsideAngular(() => {
      const checkScroll = () => {
        const s = window.scrollY > 4;
        if (s !== this.scrolled()) {
          this.zone.run(() => this.scrolled.set(s));
        }
      };
      document.addEventListener('scroll', checkScroll, { passive: true });
      checkScroll();

      const reset = () => {
        if (this.idleTimer) clearTimeout(this.idleTimer);
        if (this._idleState() === 'sleep') {
          this.zone.run(() => this._idleState.set('idle'));
        }
        this.idleTimer = setTimeout(() => {
          this.zone.run(() => this._idleState.set('sleep'));
        }, IDLE_TIMEOUT_MS);
      };

      const events: (keyof DocumentEventMap)[] = ['pointermove', 'pointerdown', 'keydown'];
      events.forEach((evt) => document.addEventListener(evt, reset, { passive: true }));
      reset();

      this.destroyRef.onDestroy(() => {
        if (this.idleTimer) clearTimeout(this.idleTimer);
        document.removeEventListener('scroll', checkScroll);
        events.forEach((evt) => document.removeEventListener(evt, reset));
      });
    });
  }

  toggleTheme(): void {
    this.themeService.toggle();
  }

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

