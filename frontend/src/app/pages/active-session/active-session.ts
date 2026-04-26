import { Component, computed, signal } from '@angular/core';
import { Mascot, MascotState } from '../../shared/mascot/mascot';

@Component({
  selector: 'app-active-session',
  imports: [Mascot],
  templateUrl: './active-session.html',
  styleUrl: './active-session.css',
})
export class ActiveSession {
  // TODO: wire to real session transport state in a future phase
  readonly connectionStatus = signal<'connecting' | 'connected' | 'disconnected'>('connected');

  readonly mascotState = computed<MascotState>(() => {
    switch (this.connectionStatus()) {
      case 'connecting': return 'loading';
      case 'disconnected': return 'error';
      default: return 'idle';
    }
  });
}
