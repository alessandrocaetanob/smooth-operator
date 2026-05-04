import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { catchError, forkJoin, of } from 'rxjs';
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

  // Last-connected timestamps (connectionId → ISO string)
  private readonly _lastConnectedMap = signal<Map<string, string>>(new Map());
  // Reachability per connection: 'unknown' | 'up' | 'down'
  private readonly _reachabilityMap = signal<Map<string, 'unknown' | 'up' | 'down'>>(new Map());

  // Compute a map of unique Vaults (Groups) from actual VaultsService
  readonly vaultsMap = computed(() => {
    const map = new Map<string, string>();
    this.vaultsList().forEach((v) => {
      map.set(v.id, v.name);
    });
    return map;
  });

  // Performance optimization: O(1) lookups for template bindings
  readonly connectionsByVault = computed(() => {
    const map = new Map<string, Connection[]>();
    for (const c of this.list()) {
      if (c.connectionGroupId != null) {
        if (!map.has(c.connectionGroupId)) {
          map.set(c.connectionGroupId, []);
        }
        map.get(c.connectionGroupId)!.push(c);
      }
    }
    return map;
  });

  readonly vaultConnectionCounts = computed(() => {
    const counts = new Map<string, number>();
    this.connectionsByVault().forEach((connections, vaultId) => {
      counts.set(vaultId, connections.length);
    });
    return counts;
  });

  readonly filteredConnections = computed(() => {
    const vaultId = this.selectedVaultId();
    if (!vaultId) return this.list();
    return this.connectionsByVault().get(vaultId) ?? [];
  });

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    // Reset reachability on each refresh so stale dots don't linger
    this._reachabilityMap.set(new Map());
    this._lastConnectedMap.set(new Map());

    forkJoin({
      connections: this.connections.reload().pipe(catchError(() => of(null))),
      vaults: this.vaultsSvc.reload().pipe(catchError(() => of(null))),
    }).subscribe({
      next: ({ connections }) => {
        if (connections !== null) {
          this.loadLastConnected();
          this.probeAll();
        }
      },
    });
  }

  private loadLastConnected(): void {
    this.connections.getLastConnected().subscribe({
      next: (rows) => {
        const m = new Map<string, string>();
        for (const r of rows) {
          if (r.connectionId && r.lastConnectedAt) m.set(r.connectionId, r.lastConnectedAt);
        }
        this._lastConnectedMap.set(m);
      },
      error: () => {},
    });
  }

  private probeAll(): void {
    const ids = this.list().map((c) => c.id);
    if (ids.length === 0) return;

    // Mark all as unknown first so the dots appear immediately
    this._reachabilityMap.update((m) => {
      const next = new Map(m);
      for (const id of ids) next.set(id, 'unknown');
      return next;
    });

    this.connections.probeConnectionsBulk(ids).subscribe({
      next: (results) => {
        this._reachabilityMap.update((m) => {
          const next = new Map(m);
          for (const [id, reachable] of Object.entries(results)) {
            next.set(id, reachable ? 'up' : 'down');
          }
          return next;
        });
      },
      error: () => {
        this._reachabilityMap.update((m) => {
          const next = new Map(m);
          for (const id of ids) next.set(id, 'down');
          return next;
        });
      },
    });
  }

  selectVault(id: string | null): void {
    this.selectedVaultId.set(id);
  }

  connect(id: string): void {
    this.router.navigate(['/connecting', id]);
  }

  downloadFile(connection: Connection, event: MouseEvent): void {
    event.stopPropagation();
    const format = (connection.protocol || 'rdp').toLowerCase() as 'rdp' | 'ssh' | 'vnc';
    const supported: ('rdp' | 'ssh' | 'vnc')[] = ['rdp', 'ssh', 'vnc'];
    this.connections
      .downloadConnectionFile(connection.id, supported.includes(format) ? format : 'rdp')
      .subscribe({ error: () => {} });
  }

  lastConnectedLabel(id: string): string | null {
    const iso = this._lastConnectedMap().get(id);
    if (!iso) return null;
    const ts = new Date(iso).getTime();
    if (!Number.isFinite(ts)) return null;
    const diff = Math.max(0, Date.now() - ts);
    const minutes = Math.floor(diff / 60_000);
    if (minutes < 1) return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    if (days < 30) return `${days}d ago`;
    const months = Math.floor(days / 30);
    return `${months}mo ago`;
  }

  reachability(id: string): 'unknown' | 'up' | 'down' {
    return this._reachabilityMap().get(id) ?? 'unknown';
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
