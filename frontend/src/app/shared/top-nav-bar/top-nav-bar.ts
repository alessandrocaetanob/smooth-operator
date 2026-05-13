import { Component, DestroyRef, NgZone, OnInit, computed, inject, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { HttpClient } from '@angular/common/http';

import { Router, RouterLink } from '@angular/router';
import { Mascot, MascotState } from '../mascot/mascot';
import { ThemeToggle } from '../theme-toggle/theme-toggle';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { ToastService } from '../toast/toast.service';
import { LayoutService } from '../../services/layout.service';

const IDLE_TIMEOUT_MS = 3 * 60 * 1000;
const HEALTH_POLL_MS = 30_000;

@Component({
  selector: 'app-top-nav-bar',
  standalone: true,
  imports: [RouterLink, Mascot, ThemeToggle, NgClass],
  templateUrl: './top-nav-bar.html',
  styleUrl: './top-nav-bar.css',
})
export class TopNavBar implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);
  private readonly destroyRef = inject(DestroyRef);
  private readonly http = inject(HttpClient);
  readonly themeService = inject(ThemeService);
  private readonly toastService = inject(ToastService);
  private readonly layout = inject(LayoutService);

  private readonly _idleState = signal<'idle' | 'sleep'>('idle');
  private idleTimer: ReturnType<typeof setTimeout> | null = null;
  private healthTimer: ReturnType<typeof setInterval> | null = null;

  readonly health = signal<'unknown' | 'healthy' | 'down'>('unknown');

  readonly healthPillClasses = computed(() => {
    const s = this.health();
    if (s === 'healthy') return 'bg-tertiary/10 border-tertiary/30';
    if (s === 'down') return 'bg-error/10 border-error/30';
    return 'bg-on-surface-variant/10 border-on-surface-variant/30';
  });
  readonly healthDotClasses = computed(() => {
    const s = this.health();
    if (s === 'healthy') return 'bg-tertiary';
    if (s === 'down') return 'bg-error';
    return 'bg-on-surface-variant';
  });
  readonly healthTextClasses = computed(() => {
    const s = this.health();
    if (s === 'healthy') return 'text-tertiary';
    if (s === 'down') return 'text-error';
    return 'text-on-surface-variant';
  });

  readonly navMascotState = computed<MascotState>(() =>
    this.toastService.toasts().some((t) => t.kind === 'error') ? 'error' : this._idleState(),
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

  readonly searchTerm = signal('');
  readonly mobileNavOpen = this.layout.mobileNavOpen;

  menuOpen = false;

  private readonly idleEvents: (keyof DocumentEventMap)[] = [
    'pointermove',
    'pointerdown',
    'keydown',
  ];
  private readonly boundCheckScroll = (): void => this.handleScroll();
  private readonly boundResetIdle = (): void => this.resetIdleTimer();

  onSearchInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.searchTerm.set(input.value);
  }

  submitSearch(): void {
    const q = this.searchTerm().trim();
    if (!q) return;
    this.layout.closeMobileNav();
    this.router.navigate(['/search'], { queryParams: { q } });
  }

  toggleMobileNav(): void {
    this.layout.toggleMobileNav();
  }

  ngOnInit(): void {
    this.zone.runOutsideAngular(() => this.setupListenersOutsideZone());

    // Poll backend /health every 30s to drive the System Status pill.
    this.checkHealth();
    this.healthTimer = setInterval(() => this.checkHealth(), HEALTH_POLL_MS);
  }

  private setupListenersOutsideZone(): void {
    document.addEventListener('scroll', this.boundCheckScroll, { passive: true });
    this.handleScroll();

    this.idleEvents.forEach((evt) =>
      document.addEventListener(evt, this.boundResetIdle, { passive: true }),
    );
    this.resetIdleTimer();

    this.destroyRef.onDestroy(() => this.cleanupListeners());
  }

  private handleScroll(): void {
    const s = window.scrollY > 4;
    if (s !== this.scrolled()) {
      this.zone.run(() => this.scrolled.set(s));
    }
  }

  private cleanupListeners(): void {
    if (this.idleTimer) clearTimeout(this.idleTimer);
    if (this.healthTimer) clearInterval(this.healthTimer);
    document.removeEventListener('scroll', this.boundCheckScroll);
    this.idleEvents.forEach((evt) => document.removeEventListener(evt, this.boundResetIdle));
  }

  private checkHealth(): void {
    this.http.get('/health', { responseType: 'text' }).subscribe({
      next: () => this.health.set('healthy'),
      error: () => this.health.set('down'),
    });
  }

  private resetIdleTimer(): void {
    if (this.idleTimer) clearTimeout(this.idleTimer);
    if (this._idleState() === 'sleep') {
      this.zone.run(() => this._idleState.set('idle'));
    }
    this.idleTimer = setTimeout(() => {
      this.zone.run(() => this._idleState.set('sleep'));
    }, IDLE_TIMEOUT_MS);
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
    this.layout.closeMobileNav();
    this.router.navigate(['/login']);
  }
}
