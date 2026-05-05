import { Component, DestroyRef, OnDestroy, OnInit, computed, effect, inject } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs/operators';
import { CommonModule } from '@angular/common';
import { GuacamoleSessionManagerService, GuacState } from '../../services/guacamole.service';
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
  private readonly sessionManager = inject(GuacamoleSessionManagerService);
  private readonly connections = inject(ConnectionsService);
  private readonly destroyRef = inject(DestroyRef);

  private navTimerId: ReturnType<typeof setTimeout> | null = null;

  readonly steps = STEP_ORDER;

  // Reactive route param — reactive to param changes without component re-creation.
  readonly connectionId = toSignal(this.route.paramMap.pipe(map((p) => p.get('id'))), {
    initialValue: this.route.snapshot.paramMap.get('id'),
  });

  // Read session signals via the manager map so reactivity is maintained.
  readonly state = computed(() => {
    const id = this.connectionId();
    if (!id) return 'idle' as GuacState;
    return this.sessionManager.sessions().get(id)?.state() ?? ('idle' as GuacState);
  });
  readonly progress = computed(() => {
    const id = this.connectionId();
    if (!id) return 0;
    return this.sessionManager.sessions().get(id)?.progress() ?? 0;
  });
  readonly logs = computed(() => {
    const id = this.connectionId();
    if (!id) return [];
    return this.sessionManager.sessions().get(id)?.logs() ?? [];
  });
  readonly errorMsg = computed(() => {
    const id = this.connectionId();
    if (!id) return null;
    return this.sessionManager.sessions().get(id)?.error() ?? null;
  });

  readonly connection = computed<Connection | null>(() => {
    const id = this.connectionId();
    if (!id) return null;
    return this.connections.listAsMap().get(id) ?? null;
  });

  readonly mascotState = computed<MascotState>(() => {
    const s = this.state();
    switch (s) {
      case 'connected':
        return 'connected';
      case 'error':
        return 'crashed';
      case 'disconnected':
        return this.errorMsg() ? 'crashed' : 'thinking';
      case 'requesting-ticket':
      case 'connecting':
      case 'waiting':
        return 'thinking';
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
    if (this.connections.list().length === 0) {
      this.connections
        .reload()
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          error: () => {
            /* intentional no-op */
          },
        });
    }
    const session = this.sessionManager.getOrCreate(id);
    const s = session.state();
    if (s !== 'connecting' && s !== 'waiting' && s !== 'connected' && s !== 'requesting-ticket') {
      session.connect().catch(() => {
        // Errors land in session.error() and surface in the log feed.
      });
    }
  }

  ngOnDestroy(): void {
    if (this.navTimerId !== null) {
      clearTimeout(this.navTimerId);
      this.navTimerId = null;
    }
    // Don't disconnect on destroy — navigating to /session/:id reuses the session.
  }

  cancel(): void {
    const id = this.connectionId();
    if (id) this.sessionManager.destroy(id);
    this.router.navigate(['/vault']);
  }

  retry(): void {
    const id = this.connectionId();
    if (!id) return;
    const session = this.sessionManager.getOrCreate(id);
    session.disconnect();
    session.reset();
    session.connect().catch(() => {
      /* intentional no-op */
    });
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
