import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgClass } from '@angular/common';
import { forkJoin, Observable, of } from 'rxjs';
import { switchMap, catchError } from 'rxjs/operators';
import {
  Connection,
  ConnectionsService,
  CreateConnectionPayload,
} from '../../services/connections.service';
import { HostsService, AppHost } from '../../services/hosts.service';
import { CredentialsService, Credential } from '../../services/credentials.service';
import { Vault, VaultsService } from '../../services/vaults.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';
import { Mascot, MascotState } from '../../shared/mascot/mascot';
import { Drawer } from '../../shared/drawer/drawer';
import { Spinner } from '../../shared/spinner/spinner';

interface FormState {
  id: string | null;
  name: string;
  protocol: string;
  hostId: string;
  connectionGroupId: string;
  credentialId: string;
  settings: string;
  newHostAddress: string;
  tags: string[];
}

interface TermColorScheme {
  value: string;
  label: string;
  preview: string;
}

const TERM_COLOR_SCHEMES: TermColorScheme[] = [
  { value: 'gray-black', label: 'Gray / Black', preview: '#111' },
  { value: 'green-black', label: 'Green / Black', preview: '#0d1a0d' },
  { value: 'white-black', label: 'White / Black', preview: '#1a1a1a' },
  { value: 'black-white', label: 'Black / White', preview: '#f5f5f5' },
  { value: 'solarized-dark', label: 'Solarized Dark', preview: '#002b36' },
  { value: 'solarized-light', label: 'Solarized Light', preview: '#fdf6e3' },
];

const TERM_FONT_NAMES = [
  'monospace',
  'Courier New',
  'Consolas',
  'DejaVu Sans Mono',
  'Source Code Pro',
  'Inconsolata',
];

const EMPTY_FORM: FormState = {
  id: null,
  name: '',
  protocol: 'rdp',
  hostId: '',
  connectionGroupId: '',
  credentialId: '',
  settings: '',
  newHostAddress: '',
  tags: [],
};

/** Simple deterministic color bucket for tag chips */
const TAG_PALETTES = [
  'bg-primary/10 text-primary border-primary/20',
  'bg-secondary/10 text-secondary border-secondary/20',
  'bg-tertiary/10 text-tertiary border-tertiary/20',
  'bg-amber-500/10 text-amber-600 border-amber-500/20 dark:text-amber-400',
  'bg-violet-500/10 text-violet-600 border-violet-500/20 dark:text-violet-400',
  'bg-rose-500/10 text-rose-600 border-rose-500/20 dark:text-rose-400',
];

@Component({
  selector: 'app-connections',
  imports: [FormsModule, NgClass, Mascot, Drawer, Spinner],
  templateUrl: './connections.html',
  styleUrl: './connections.css',
})
export class Connections implements OnInit {
  private readonly connectionsSvc = inject(ConnectionsService);
  private readonly hostsSvc = inject(HostsService);
  private readonly credentialsSvc = inject(CredentialsService);
  private readonly vaultsSvc = inject(VaultsService);
  private readonly confirmSvc = inject(ConfirmDialogService);
  private readonly toastSvc = inject(ToastService);

  readonly connections = this.connectionsSvc.list;
  readonly hosts = this.hostsSvc.list;
  readonly credentials = this.credentialsSvc.list;
  readonly vaults = this.vaultsSvc.list;

  // Performance optimization: O(1) lookups for template bindings
  readonly hostsMap = computed(() => new Map(this.hosts().map((h) => [h.id, h])));
  readonly credentialsMap = computed(() => new Map(this.credentials().map((c) => [c.id, c])));
  readonly vaultsMap = computed(() => new Map(this.vaults().map((v) => [v.id, v])));

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showDrawer = signal(false);
  readonly form = signal<FormState>({ ...EMPTY_FORM });
  readonly busy = signal(false);
  readonly mascotState = signal<MascotState>('idle');
  readonly tagInputValue = signal('');

  // Terminal appearance (SSH only)
  readonly termColorScheme = signal('gray-black');
  readonly termFontName = signal('monospace');
  readonly termFontSize = signal(12);
  readonly termColorSchemes = TERM_COLOR_SCHEMES;
  readonly termFontNames = TERM_FONT_NAMES;

  // Search & filter
  readonly searchQuery = signal('');
  readonly activeTagFilter = signal('');

  readonly protocols = ['rdp', 'ssh', 'vnc'];

  /** Placeholder for the Advanced Settings textarea — protocol-specific */
  readonly settingsPlaceholder = computed(() => {
    const proto = this.form().protocol as keyof typeof this.settingsExamples;
    return this.settingsExamples[proto] ?? this.settingsExamples.rdp;
  });
  readonly settingsExamples = {
    rdp: '{\n  "port": 3389,\n  "security": "nla",\n  "ignore-cert": true,\n  "width": 1920,\n  "height": 1080\n}',
    ssh: '{\n  "port": 22,\n  "terminal-type": "xterm",\n  "command": "/bin/bash"\n}',
    vnc: '{\n  "port": 5900,\n  "encoding": "zrle",\n  "read-only": false\n}',
  };

  /** All unique tags across all connections, sorted */
  readonly allTags = computed(() => {
    const tagSet = new Set<string>();
    this.connections().forEach((c) => c.tags?.forEach((t) => tagSet.add(t)));
    return Array.from(tagSet).sort((a, b) => a.localeCompare(b));
  });

  /** Pre-compute flattened, lowercase search index strings to avoid repeated O(N) array iteration and string manipulation during keystroke filtering */
  readonly searchIndexMap = computed(() => {
    const map = new Map<string, string>();
    this.connections().forEach((c) => {
      const parts = [
        c.name,
        c.protocol,
        c.host?.address ?? '',
        c.host?.name ?? '',
        ...(c.tags ?? []),
      ];
      map.set(c.id, parts.join(' ').toLowerCase());
    });
    return map;
  });

  /** Filtered connection list based on search query and tag filter */
  readonly filteredConnections = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    const tagFilter = this.activeTagFilter();
    const conns = this.connections();
    const indexMap = this.searchIndexMap();

    return conns.filter((c) => {
      if (tagFilter && !c.tags?.includes(tagFilter)) return false;
      if (!q) return true;

      const indexStr = indexMap.get(c.id);
      return indexStr ? indexStr.includes(q) : false;
    });
  });

  readonly resultCount = computed(() => {
    const total = this.connections().length;
    const filtered = this.filteredConnections().length;
    return total === filtered ? total : filtered;
  });

  readonly isFiltered = computed(
    () => this.searchQuery().trim().length > 0 || this.activeTagFilter().length > 0,
  );

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      conns: this.connectionsSvc.reload(),
      hosts: this.hostsSvc.reload(),
      creds: this.credentialsSvc.reload(),
      vaults: this.vaultsSvc.reload(),
    }).subscribe({
      next: () => this.loading.set(false),
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(this.toMessage(err) || 'Failed to load.');
      },
    });
  }

  newConnection(): void {
    this.form.set({ ...EMPTY_FORM });
    this.showDrawer.set(true);
  }

  edit(c: Connection): void {
    const settingsObj = this.parseSettings(c.settings || '{}');
    this.termColorScheme.set(settingsObj['color-scheme'] ?? 'gray-black');
    this.termFontName.set(settingsObj['font-name'] ?? 'monospace');
    const rawFontSize = Number(settingsObj['font-size']);
    this.termFontSize.set(Number.isFinite(rawFontSize) ? rawFontSize : 12);

    // Strip terminal appearance keys from displayed settings
    if (c.protocol === 'ssh') {
      delete settingsObj['color-scheme'];
      delete settingsObj['font-name'];
      delete settingsObj['font-size'];
    }

    this.form.set({
      id: c.id,
      name: c.name,
      protocol: c.protocol,
      hostId: c.hostId,
      connectionGroupId: c.connectionGroupId ?? '',
      credentialId: c.credentialId ?? '',
      settings: Object.keys(settingsObj).length ? JSON.stringify(settingsObj, null, 2) : '{}',
      newHostAddress: '',
      tags: [...(c.tags ?? [])],
    });
    this.showDrawer.set(true);
  }

  closeDrawer(): void {
    this.showDrawer.set(false);
    this.form.set({ ...EMPTY_FORM });
    this.errorMessage.set(null);
    this.tagInputValue.set('');
    this.mascotState.set('idle');
    this.termColorScheme.set('gray-black');
    this.termFontName.set('monospace');
    this.termFontSize.set(12);
  }

  patch<K extends keyof FormState>(key: K, value: FormState[K]): void {
    this.form.update((f) => ({ ...f, [key]: value }));
  }

  // ── Tag management ────────────────────────────────────────────────────

  addTagFromInput(): void {
    const raw = this.tagInputValue().trim().toLowerCase();
    if (!raw) return;
    const tags = raw
      .split(',')
      .map((t) => t.trim())
      .filter(Boolean);
    this.form.update((f) => {
      const next = Array.from(new Set([...f.tags, ...tags]));
      return { ...f, tags: next };
    });
    this.tagInputValue.set('');
  }

  onTagInputKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault();
      this.addTagFromInput();
    } else if (event.key === 'Backspace' && !this.tagInputValue()) {
      this.form.update((f) => ({ ...f, tags: f.tags.slice(0, -1) }));
    }
  }

  removeTag(tag: string): void {
    this.form.update((f) => ({ ...f, tags: f.tags.filter((t) => t !== tag) }));
  }

  tagColor(tag: string): string {
    const idx =
      tag.split('').reduce((a, c) => a + (c.codePointAt(0) ?? 0), 0) % TAG_PALETTES.length;
    return TAG_PALETTES[idx];
  }

  // ── Filter bar ────────────────────────────────────────────────────────

  setTagFilter(tag: string): void {
    this.activeTagFilter.set(this.activeTagFilter() === tag ? '' : tag);
  }

  clearFilters(): void {
    this.searchQuery.set('');
    this.activeTagFilter.set('');
  }

  // ── Save / Delete ─────────────────────────────────────────────────────

  save(): void {
    if (this.busy()) return;
    const f = this.form();

    if (
      !f.name.trim() ||
      (!f.hostId && !f.newHostAddress.trim()) ||
      !f.protocol ||
      !f.connectionGroupId
    ) {
      this.errorMessage.set('Name, host, protocol and vault are required.');
      this.mascotState.set('error');
      return;
    }

    this.busy.set(true);
    this.errorMessage.set(null);
    this.mascotState.set('thinking');

    const getHostId$ = (): Observable<string> => {
      if (f.hostId) {
        return of(f.hostId);
      } else {
        const newHostName = f.name.trim() + ' Host';
        return this.hostsSvc.create({ name: newHostName, address: f.newHostAddress.trim() }).pipe(
          switchMap(() => this.hostsSvc.reload()),
          switchMap((hosts) => {
            const created = hosts.find((h) => h.address === f.newHostAddress.trim());
            if (!created) throw new Error('Failed to locate created host');
            return of(created.id);
          }),
        );
      }
    };

    getHostId$()
      .pipe(
        switchMap((resolvedHostId) => {
          let settingsJson = f.settings || '{}';
          if (f.protocol === 'ssh') {
            const obj = this.parseSettings(settingsJson);
            obj['color-scheme'] = this.termColorScheme();
            obj['font-name'] = this.termFontName();
            obj['font-size'] = String(this.termFontSize());
            settingsJson = JSON.stringify(obj);
          }
          const payload: CreateConnectionPayload = {
            name: f.name.trim(),
            protocol: f.protocol,
            hostId: resolvedHostId,
            connectionGroupId: f.connectionGroupId || null,
            credentialId: f.credentialId || null,
            settings: settingsJson,
            tags: f.tags,
          };

          return f.id
            ? this.connectionsSvc.update(f.id, payload)
            : this.connectionsSvc.create(payload);
        }),
        catchError((err) => {
          throw err;
        }),
      )
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.mascotState.set('success');
          this.toastSvc.success(f.id ? 'Connection updated.' : 'Connection created.');
          setTimeout(() => this.closeDrawer(), 800);
          this.refresh();
        },
        error: (err: unknown) => {
          this.busy.set(false);
          this.mascotState.set('error');
          const msg = this.toMessage(err) || 'Save failed.';
          this.errorMessage.set(msg);
          this.toastSvc.error(msg);
        },
      });
  }

  async remove(c: Connection): Promise<void> {
    const ok = await this.confirmSvc.ask({
      title: 'Delete connection',
      message: `Delete connection "${c.name}"? This cannot be undone.`,
      confirmLabel: 'Delete',
      tone: 'danger',
    });
    if (!ok) return;
    this.errorMessage.set(null);
    this.connectionsSvc.remove(c.id).subscribe({
      next: () => {
        this.toastSvc.success(`Connection "${c.name}" deleted.`);
        this.refresh();
      },
      error: (err) => {
        const msg = this.toMessage(err) || 'Delete failed.';
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
      },
    });
  }

  trackById(_: number, c: Connection): string {
    return c.id;
  }

  trackHost(_: number, h: AppHost): string {
    return h.id;
  }

  trackCred(_: number, c: Credential): string {
    return c.id;
  }

  trackVault(_: number, v: Vault): string {
    return v.id;
  }

  hostName(id: string): string {
    return this.hostsMap().get(id)?.name ?? '—';
  }

  credentialName(id: string | null | undefined): string {
    if (!id) return '—';
    return this.credentialsMap().get(id)?.name ?? '—';
  }

  vaultName(id: string | null | undefined): string {
    if (!id) return '—';
    return this.vaultsMap().get(id)?.name ?? '—';
  }

  private parseSettings(json: string): Record<string, string> {
    try {
      return JSON.parse(json) ?? {};
    } catch {
      return {};
    }
  }

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
