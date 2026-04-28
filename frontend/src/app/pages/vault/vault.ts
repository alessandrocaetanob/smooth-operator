import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { Connection, ConnectionsService } from '../../services/connections.service';
import { AuthService } from '../../services/auth.service';
import { VaultsService } from '../../services/vaults.service';
import { Mascot } from '../../shared/mascot/mascot';
import { listStagger } from '../../shared/animations';

@Component({
  selector: 'app-vault',
  standalone: true,
  imports: [RouterLink, CommonModule, Mascot],
  templateUrl: './vault.html',
  styleUrl: './vault.css',
  animations: [listStagger],
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

  protocolIconBg(protocol: string): string {
    switch ((protocol || '').toLowerCase()) {
      case 'rdp':
        return 'bg-blue-500/10 border-blue-500/30 group-hover:bg-blue-500/20';
      case 'ssh':
        return 'bg-green-500/10 border-green-500/30 group-hover:bg-green-500/20';
      case 'vnc':
        return 'bg-purple-500/10 border-purple-500/30 group-hover:bg-purple-500/20';
      default:
        return 'bg-surface-container border-outline-variant/30 group-hover:bg-primary-container group-hover:border-primary/30';
    }
  }

  protocolIconColor(protocol: string): string {
    switch ((protocol || '').toLowerCase()) {
      case 'rdp':
        return 'text-blue-400 group-hover:text-blue-300';
      case 'ssh':
        return 'text-green-400 group-hover:text-green-300';
      case 'vnc':
        return 'text-purple-400 group-hover:text-purple-300';
      default:
        return 'text-on-surface-variant group-hover:text-primary';
    }
  }

  protocolFooterBg(protocol: string): string {
    switch ((protocol || '').toLowerCase()) {
      case 'rdp':
        return 'bg-blue-500/5 border-blue-500/10';
      case 'ssh':
        return 'bg-green-500/5 border-green-500/10';
      case 'vnc':
        return 'bg-purple-500/5 border-purple-500/10';
      default:
        return 'bg-surface-container-low/50';
    }
  }

  protocolBadgeColor(protocol: string): string {
    switch ((protocol || '').toLowerCase()) {
      case 'rdp':
        return 'text-blue-400';
      case 'ssh':
        return 'text-green-400';
      case 'vnc':
        return 'text-purple-400';
      default:
        return 'text-on-surface-variant/80';
    }
  }
}
