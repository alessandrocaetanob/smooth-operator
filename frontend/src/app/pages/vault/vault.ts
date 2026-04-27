import { Component, OnInit, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ConnectionsService } from '../../services/connections.service';
import { AuthService } from '../../services/auth.service';
import { Mascot } from '../../shared/mascot/mascot';

@Component({
  selector: 'app-vault',
  imports: [RouterLink, Mascot],
  templateUrl: './vault.html',
  styleUrl: './vault.css',
})
export class Vault implements OnInit {
  private readonly connections = inject(ConnectionsService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly list = this.connections.list;
  readonly loading = this.connections.loading;
  readonly hasConnections = computed(() => this.list().length > 0);
  readonly canManageConnections = this.auth.canManageConnections;

  ngOnInit(): void {
    this.connections.reload().subscribe({ error: () => {} });
  }

  refresh(): void {
    this.connections.reload().subscribe({ error: () => {} });
  }

  connect(id: string): void {
    this.router.navigate(['/session'], { queryParams: { id } });
  }

  protocolIcon(protocol: string): string {
    switch ((protocol || '').toLowerCase()) {
      case 'rdp':
        return 'desktop_windows';
      case 'ssh':
        return 'terminal';
      case 'vnc':
        return 'screen_share';
      default:
        return 'hub';
    }
  }
}
