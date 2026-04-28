import { Component, DestroyRef, OnDestroy, OnInit, computed, effect, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { GuacamoleClientService, GuacState } from '../../services/guacamole.service';
import { ConnectionsService, Connection } from '../../services/connections.service';
import { Mascot, MascotState } from '../../shared/mascot/mascot';

interface Step {
  key: GuacState;
  label: string;
}

const STEP_ORDER: Step[] = [
  { key: 'requesting-ticket', label: 'INITIATING' },
  { key: 'connecting', label: 'CONNECTING' },
  { key: 'waiting', label: 'AUTHENTICATING' },
  { key: 'connected', label: 'READY' },
];

@Component({
  selector: 'app-connecting-state',
  imports: [CommonModule, Mascot],
  templateUrl: './connecting-state.html',
  styleUrl: './connecting-state.css',
})
export class ConnectingState implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly guac = inject(GuacamoleClientService);
  private readonly connections = inject(ConnectionsService);
  private readonly destroyRef = inject(DestroyRef);

  private navTimerId: ReturnType<typeof setTimeout> | null = null;

  readonly steps = STEP_ORDER;
  readonly state = this.guac.state;
  readonly progress = this.guac.progress;
  readonly logs = this.guac.logs;
  readonly errorMsg = this.guac.error;
  readonly connection = computed<Connection | null>(() => {
    const id = this.connectionId();
    if (!id) return null;
    return this.connections.list().find((c) => c.id === id) ?? null;
  });
  readonly connectionId = computed(() => this.route.snapshot.paramMap.get('id'));

  readonly mascotState = computed<MascotState>(() => {
    const s = this.state();
    switch (s) {
      case 'connected':
        return 'connected';
      case 'error':
        return 'error';
      case 'requesting-ticket':
      case 'connecting':
      case 'waiting':
        return 'loading';
      default:
        return 'thinking';
    }
  });

  readonly statusLabel = computed<string>(() => {
    const s = this.state();
    switch (s) {
      case 'requesting-ticket':
        return 'INITIATING';
      case 'connecting':
        return 'CONNECTING';
      case 'waiting':
        return 'AUTHENTICATING';
      case 'connected':
        return 'READY';
      case 'error':
        return 'ERROR';
      case 'disconnected':
        return 'DISCONNECTED';
      default:
        return 'IDLE';
    }
  });

  constructor() {
    // When the client reaches CONNECTED, transition to ActiveSession.
    effect(() => {
      const s = this.state();
      const id = this.connectionId();
      if (s === 'connected' && id) {
        // Defer to next macrotask so the bound view reflects READY before nav.
        this.navTimerId = setTimeout(() => this.router.navigate(['/session', id]), 350);
      }
    });
  }

  ngOnInit(): void {
    const id = this.connectionId();
    if (!id) {
      this.router.navigate(['/vault']);
      return;
    }
    // Make sure the connection list is populated so we can show the name.
    if (this.connections.list().length === 0) {
      this.connections
        .reload()
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({ error: () => {} });
    }
    // Don't restart a connection that's already in flight for the same target
    // (e.g., happens during route transition into ActiveSession on reconnect).
    if (
      this.state() !== 'connecting' &&
      this.state() !== 'waiting' &&
      this.state() !== 'connected'
    ) {
      this.guac.connect(id).catch(() => {
        // Errors land in service.error and surface in the log feed.
      });
    }
  }

  ngOnDestroy(): void {
    if (this.navTimerId !== null) {
      clearTimeout(this.navTimerId);
      this.navTimerId = null;
    }
    // We don't disconnect on destroy because navigating to /session/:id
    // mounts ActiveSession which keeps using the same client instance.
  }

  cancel(): void {
    this.guac.disconnect();
    this.guac.reset();
    this.router.navigate(['/vault']);
  }

  retry(): void {
    const id = this.connectionId();
    if (!id) return;
    this.guac.disconnect();
    this.guac.reset();
    this.guac.connect(id).catch(() => {});
  }

  isStepDone(step: Step): boolean {
    return this.stepIndex(this.state()) > this.stepIndex(step.key);
  }
  isStepActive(step: Step): boolean {
    return this.state() === step.key;
  }

  private stepIndex(s: GuacState): number {
    const i = this.steps.findIndex((x) => x.key === s);
    return i === -1 ? -1 : i;
  }

  logColorClass(level: string): string {
    switch (level) {
      case 'ok':
        return 'text-green-500';
      case 'warn':
        return 'text-amber-400';
      case 'error':
        return 'text-error';
      default:
        return 'text-primary';
    }
  }
  logTag(level: string): string {
    switch (level) {
      case 'ok':
        return '[OK]';
      case 'warn':
        return '[WRN]';
      case 'error':
        return '[ERR]';
      default:
        return '[..]';
    }
  }
}
