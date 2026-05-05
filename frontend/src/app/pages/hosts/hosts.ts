import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HostsService, AppHost, CreateHostPayload } from '../../services/hosts.service';
import { AuthService } from '../../services/auth.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';
import { Drawer } from '../../shared/drawer/drawer';

interface FormState {
  id: string | null;
  name: string;
  address: string;
}

const EMPTY_FORM: FormState = {
  id: null,
  name: '',
  address: '',
};

@Component({
  selector: 'app-hosts',
  imports: [FormsModule, Drawer],
  templateUrl: './hosts.html',
  styleUrl: './hosts.css',
})
export class Hosts implements OnInit {
  private readonly svc = inject(HostsService);
  private readonly auth = inject(AuthService);
  private readonly confirmSvc = inject(ConfirmDialogService);
  private readonly toastSvc = inject(ToastService);

  readonly hosts = this.svc.list;
  readonly canManageConnections = this.auth.canManageConnections;
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showDrawer = signal(false);
  readonly form = signal<FormState>({ ...EMPTY_FORM });
  readonly busy = signal(false);
  readonly searchQuery = signal('');

  readonly filteredHosts = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    if (!q) return this.hosts();
    return this.hosts().filter(
      (h) => h.name.toLowerCase().includes(q) || h.address.toLowerCase().includes(q),
    );
  });

  readonly isFiltered = computed(() => this.searchQuery().trim().length > 0);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.svc.reload().subscribe({
      next: () => this.loading.set(false),
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(this.toMessage(err) || 'Failed to load.');
      },
    });
  }

  newHost(): void {
    if (!this.canManageConnections()) return;
    this.form.set({ ...EMPTY_FORM });
    this.errorMessage.set(null);
    this.showDrawer.set(true);
  }

  edit(h: AppHost): void {
    if (!this.canManageConnections()) return;
    this.form.set({ id: h.id, name: h.name, address: h.address });
    this.errorMessage.set(null);
    this.showDrawer.set(true);
  }

  closeDrawer(): void {
    this.showDrawer.set(false);
    this.form.set({ ...EMPTY_FORM });
    this.errorMessage.set(null);
  }

  patch<K extends keyof FormState>(key: K, value: FormState[K]): void {
    this.form.update((f) => ({ ...f, [key]: value }));
  }

  save(): void {
    if (!this.canManageConnections()) return;
    if (this.busy()) return;
    const f = this.form();
    if (!f.name.trim() || !f.address.trim()) {
      this.errorMessage.set('Name and address are required.');
      return;
    }
    this.busy.set(true);
    this.errorMessage.set(null);

    const payload: CreateHostPayload = { name: f.name.trim(), address: f.address.trim() };

    if (f.id) {
      this.svc.update(f.id, payload).subscribe({
        next: () => this.done(),
        error: (err) => this.fail(err),
      });
    } else {
      this.svc.create(payload).subscribe({
        next: () => this.done(),
        error: (err) => this.fail(err),
      });
    }
  }

  async remove(h: AppHost): Promise<void> {
    if (!this.canManageConnections()) return;
    const ok = await this.confirmSvc.ask({
      title: 'Delete host',
      message: `Delete host "${h.name}"? Connections using this host will break. This cannot be undone.`,
      confirmLabel: 'Delete',
      tone: 'danger',
    });
    if (!ok) return;
    this.errorMessage.set(null);
    this.svc.remove(h.id).subscribe({
      next: () => {
        this.toastSvc.success(`Host "${h.name}" deleted.`);
        this.refresh();
      },
      error: (err) => {
        const msg = this.toMessage(err) || 'Delete failed.';
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
      },
    });
  }

  trackById(_: number, h: AppHost): string {
    return h.id;
  }

  private done(): void {
    this.busy.set(false);
    this.toastSvc.success('Host saved.');
    this.closeDrawer();
    this.refresh();
  }

  private fail(err: unknown): void {
    this.busy.set(false);
    const msg = this.toMessage(err) || 'Save failed.';
    this.errorMessage.set(msg);
    this.toastSvc.error(msg);
  }

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
