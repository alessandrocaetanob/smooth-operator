import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs/operators';
import { VaultsService, Vault } from '../../services/vaults.service';
import { ConnectionsService, Connection } from '../../services/connections.service';

interface VaultMatch {
  kind: 'vault';
  id: string;
  name: string;
  subtitle: string;
}

interface ConnectionMatch {
  kind: 'connection';
  id: string;
  name: string;
  subtitle: string;
  protocol: string;
}

type Match = VaultMatch | ConnectionMatch;

@Component({
  selector: 'app-search-results',
  standalone: true,
  imports: [],
  templateUrl: './search-results.html',
  styleUrl: './search-results.css',
})
export class SearchResults {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly vaults = inject(VaultsService);
  private readonly connections = inject(ConnectionsService);

  readonly query = toSignal(
    this.route.queryParamMap.pipe(map((p) => (p.get('q') ?? '').trim())),
    { initialValue: '' },
  );

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  private readonly _vaults = signal<Vault[]>([]);
  private readonly _connections = signal<Connection[]>([]);

  readonly vaultMatches = computed<VaultMatch[]>(() => {
    const q = this.query().toLowerCase();
    if (!q) return [];
    return this._vaults()
      .filter((v) => v.name?.toLowerCase().includes(q))
      .map((v) => ({
        kind: 'vault' as const,
        id: v.id,
        name: v.name,
        subtitle: this.vaultSubtitle(v),
      }));
  });

  readonly connectionMatches = computed<ConnectionMatch[]>(() => {
    const q = this.query().toLowerCase();
    if (!q) return [];
    return this._connections()
      .filter(
        (c) =>
          c.name?.toLowerCase().includes(q) ||
          c.host?.name?.toLowerCase().includes(q) ||
          c.host?.address?.toLowerCase().includes(q) ||
          c.protocol?.toLowerCase().includes(q),
      )
      .map((c) => ({
        kind: 'connection' as const,
        id: c.id,
        name: c.name,
        protocol: (c.protocol ?? '').toUpperCase(),
        subtitle: this.connectionSubtitle(c),
      }));
  });

  readonly totalCount = computed(
    () => this.vaultMatches().length + this.connectionMatches().length,
  );

  readonly hasQuery = computed(() => this.query().length > 0);

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    let vaultsDone = false;
    let connsDone = false;
    const finish = () => {
      if (vaultsDone && connsDone) this.loading.set(false);
    };

    this.vaults.reload().subscribe({
      next: (rows) => {
        this._vaults.set(rows ?? []);
        vaultsDone = true;
        finish();
      },
      error: () => {
        vaultsDone = true;
        finish();
      },
    });

    this.connections.reload().subscribe({
      next: (rows) => {
        this._connections.set(rows ?? []);
        connsDone = true;
        finish();
      },
      error: () => {
        connsDone = true;
        finish();
      },
    });
  }

  private vaultSubtitle(v: Vault): string {
    const parts: string[] = [];
    if (v.userCount != null) parts.push(`${v.userCount} user${v.userCount === 1 ? '' : 's'}`);
    if (v.groupCount != null) parts.push(`${v.groupCount} group${v.groupCount === 1 ? '' : 's'}`);
    return parts.join(' · ') || 'Vault';
  }

  private connectionSubtitle(c: Connection): string {
    if (c.host?.address) return `${c.host.name ?? c.host.address} · ${c.host.address}`;
    if (c.host?.name) return c.host.name;
    return 'Connection';
  }

  openVault(id: string): void {
    this.router.navigate(['/vault'], { queryParams: { vaultId: id } });
  }

  openConnection(id: string): void {
    this.router.navigate(['/connections'], { queryParams: { connectionId: id } });
  }
}
