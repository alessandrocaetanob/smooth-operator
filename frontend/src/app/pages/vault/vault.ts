import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { Connection, ConnectionsService } from '../../services/connections.service';
import { AuthService } from '../../services/auth.service';
import { VaultsService } from '../../services/vaults.service';
import { Mascot } from '../../shared/mascot/mascot';

@Component({
  selector: 'app-vault',
  standalone: true,
  imports: [RouterLink, CommonModule, Mascot],
  templateUrl: './vault.html',
  styleUrl: './vault.css',
})
export class Vault implements OnInit {
  private readonly connections = inject(ConnectionsService);
  private readonly vaultsSvc = inject(VaultsService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly list = this.connections.list;
  readonly vaultsList = this.vaultsSvc.list;
  readonly loading = this.connections.loading;
  readonly hasConnections = computed(() => this.list().length > 0);
  readonly canManageConnections = this.auth.canManageConnections;

  // State for sidebar filtering
  readonly selectedVaultId = signal<string | null>(null);

  // Compute a map of unique Vaults (Groups) from actual VaultsService
  readonly vaultsMap = computed(() => {
    const map = new Map<string, string>();
    this.vaultsList().forEach((v) => {
      map.set(v.id, v.name);
    });
    return map;
  });

  readonly filteredConnections = computed(() => {
    const vaultId = this.selectedVaultId();
    if (!vaultId) return this.list();
    return this.list().filter((c) => c.connectionGroupId === vaultId);
  });

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.connections.reload().subscribe({ error: () => {} });
    this.vaultsSvc.reload().subscribe({ error: () => {} });
  }

  selectVault(id: string | null): void {
    this.selectedVaultId.set(id);
  }

  getConnectionsForVault(vaultId: string): Connection[] {
    return this.list().filter((c) => c.connectionGroupId === vaultId);
  }

  connect(id: string): void {
    this.router.navigate(['/connecting', id]);
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
