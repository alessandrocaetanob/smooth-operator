import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { GuacamoleSessionManagerService, GuacamoleSession } from '../../services/guacamole.service';
import { ConnectionsService } from '../../services/connections.service';

@Component({
  selector: 'app-session-bar',
  imports: [],
  templateUrl: './session-bar.html',
  styleUrl: './session-bar.css',
})
export class SessionBar {
  private readonly router = inject(Router);
  private readonly sessionManager = inject(GuacamoleSessionManagerService);
  private readonly connections = inject(ConnectionsService);

  readonly minimizedSessions = this.sessionManager.minimizedSessions;

  connectionName(session: GuacamoleSession): string {
    return (
      this.connections.listAsMap().get(session.connectionId)?.name ??
      session.displayName() ??
      session.connectionId
    );
  }

  restore(session: GuacamoleSession): void {
    session.minimized.set(false);
    this.router.navigate(['/session', session.connectionId]);
  }

  close(session: GuacamoleSession, event: Event): void {
    event.stopPropagation();
    this.sessionManager.destroy(session.connectionId);
  }
}
