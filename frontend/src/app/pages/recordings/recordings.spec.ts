import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { RecordingsPage } from './recordings';
import { Recording, RecordingsList, RecordingsService } from '../../services/recordings.service';
import { ToastService } from '../../shared/toast/toast.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { AuthService } from '../../services/auth.service';

describe('RecordingsPage', () => {
  let component: RecordingsPage;
  let fixture: ComponentFixture<RecordingsPage>;
  let recordings: {
    list: ReturnType<typeof vi.fn>;
    streamUrl: ReturnType<typeof vi.fn>;
    remove: ReturnType<typeof vi.fn>;
  };
  let toast: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };
  let confirm: { ask: ReturnType<typeof vi.fn> };
  let auth: { currentUser: ReturnType<typeof signal<{ roles: string[] } | null>> };

  const makeRec = (over: Partial<Recording> = {}): Recording => ({
    id: 'r1',
    sessionId: 'sess-1',
    connectionId: 'c1',
    connectionName: 'web-1',
    protocol: 'rdp',
    vaultId: null,
    vaultName: null,
    userId: 'u1',
    userEmail: 'alice@x',
    startedAt: '2026-05-10T10:00:00Z',
    endedAt: '2026-05-10T10:30:00Z',
    durationSeconds: 1800,
    storageType: 'Local',
    fileSizeBytes: 4096,
    status: 'Available',
    errorMessage: null,
    includeKeys: false,
    ...over,
  });

  const makeList = (over: Partial<RecordingsList> = {}): RecordingsList => ({
    total: 1,
    page: 1,
    pageSize: 25,
    items: [makeRec()],
    ...over,
  });

  beforeEach(async () => {
    recordings = {
      list: vi.fn(() => of(makeList())),
      streamUrl: vi.fn((id: string) => `/api/recordings/${id}/stream`),
      remove: vi.fn(() => of(undefined)),
    };
    toast = { success: vi.fn(), error: vi.fn() };
    confirm = { ask: vi.fn() };
    auth = {
      currentUser: signal<{ roles: string[] } | null>({ roles: ['Admin'] }),
    };

    await TestBed.configureTestingModule({
      imports: [RecordingsPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RecordingsService, useValue: recordings },
        { provide: ToastService, useValue: toast },
        { provide: ConfirmDialogService, useValue: confirm },
        { provide: AuthService, useValue: auth },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(RecordingsPage);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    // Restore any global stubs (e.g. document.createElement in the download tests)
    // so the next TestBed.createComponent call gets a real host element.
    vi.restoreAllMocks();
  });

  it('creates and hydrates items via ngOnInit -> refresh', () => {
    component.ngOnInit();
    expect(recordings.list).toHaveBeenCalled();
    expect(component.items().length).toBe(1);
    expect(component.total()).toBe(1);
    expect(component.loading()).toBe(false);
  });

  it('refresh surfaces errors with explicit message', () => {
    recordings.list.mockReturnValueOnce(throwError(() => ({ message: 'boom' })));
    component.refresh();
    expect(component.error()).toBe('boom');
    expect(component.loading()).toBe(false);
  });

  it('refresh falls back to default error', () => {
    recordings.list.mockReturnValueOnce(throwError(() => ({})));
    component.refresh();
    expect(component.error()).toBe('Failed to load recordings.');
  });

  describe('filters and pagination', () => {
    it('applyFilters resets page to 1 and refreshes', () => {
      component.page.set(5);
      component.applyFilters();
      expect(component.page()).toBe(1);
      expect(recordings.list).toHaveBeenCalled();
    });

    it('clearFilters resets all filter signals', () => {
      component.filterConnectionId.set('c');
      component.filterVaultId.set('v');
      component.filterFrom.set('2025-01-01');
      component.filterTo.set('2025-01-02');
      component.filterStatus.set('Failed');
      component.clearFilters();
      expect(component.filterConnectionId()).toBe('');
      expect(component.filterVaultId()).toBe('');
      expect(component.filterFrom()).toBe('');
      expect(component.filterTo()).toBe('');
      expect(component.filterStatus()).toBe('');
      expect(component.page()).toBe(1);
    });

    it('passes filter signals through to the service call', () => {
      component.filterConnectionId.set('conn-7');
      component.filterVaultId.set('vault-9');
      component.filterFrom.set('2026-04-01');
      component.filterTo.set('2026-04-30');
      component.filterStatus.set('Available');
      component.page.set(3);
      component.pageSize.set(50);
      component.refresh();
      expect(recordings.list).toHaveBeenCalledWith(
        expect.objectContaining({
          connectionId: 'conn-7',
          vaultId: 'vault-9',
          from: '2026-04-01',
          to: '2026-04-30',
          status: 'Available',
          page: 3,
          pageSize: 50,
        }),
      );
    });

    it('nextPage advances when not at last page', () => {
      // The component sends the *new* page to the server in the second refresh; we assert
      // that the outgoing request increments rather than the response-driven page() signal
      // (which the mock always sets back from the response payload).
      recordings.list.mockReturnValue(of(makeList({ total: 60, page: 1, pageSize: 25 })));
      component.refresh();
      recordings.list.mockClear();
      component.nextPage();
      expect(recordings.list).toHaveBeenCalledWith(expect.objectContaining({ page: 2 }));
    });

    it('nextPage no-ops at last page', () => {
      recordings.list.mockReturnValue(of(makeList({ total: 10, page: 1, pageSize: 25 })));
      component.refresh();
      recordings.list.mockClear();
      component.nextPage();
      expect(recordings.list).not.toHaveBeenCalled();
    });

    it('prevPage decrements when above page 1', () => {
      recordings.list.mockReturnValue(of(makeList({ total: 60, page: 2, pageSize: 25 })));
      component.refresh();
      recordings.list.mockClear();
      component.prevPage();
      expect(recordings.list).toHaveBeenCalledWith(expect.objectContaining({ page: 1 }));
    });

    it('prevPage no-ops at page 1', () => {
      component.page.set(1);
      component.prevPage();
      expect(component.page()).toBe(1);
    });

    it('pageCount handles zero/non-zero pageSize', () => {
      component.pageSize.set(25);
      component.total.set(80);
      expect(component.pageCount()).toBe(4);
      component.pageSize.set(0);
      expect(component.pageCount()).toBe(0);
    });
  });

  describe('player open / close', () => {
    it('open sets playerOpenFor only when status is Available', () => {
      const rec = makeRec();
      component.open(rec);
      expect(component.playerOpenFor()).toBe(rec);
      component.closePlayer();
      expect(component.playerOpenFor()).toBeNull();
    });

    it('open is a no-op for non-Available recordings', () => {
      component.open(makeRec({ status: 'Uploading' }));
      expect(component.playerOpenFor()).toBeNull();
    });
  });

  describe('download', () => {
    it('triggers an anchor click with the streamUrl for Available recordings', () => {
      const clickSpy = vi.fn();
      const removeSpy = vi.fn();
      const createSpy = vi.spyOn(document, 'createElement').mockImplementation(
        () =>
          ({
            click: clickSpy,
            remove: removeSpy,
            setAttribute: vi.fn(),
          }) as unknown as HTMLAnchorElement,
      );
      vi.spyOn(document.body, 'appendChild').mockImplementation((node) => node);

      component.download(makeRec());

      expect(createSpy).toHaveBeenCalledWith('a');
      expect(clickSpy).toHaveBeenCalled();
      expect(recordings.streamUrl).toHaveBeenCalledWith('r1');
    });

    it('is a no-op for non-Available recordings', () => {
      const createSpy = vi.spyOn(document, 'createElement');
      component.download(makeRec({ status: 'Failed' }));
      expect(createSpy).not.toHaveBeenCalled();
    });
  });

  describe('remove', () => {
    it('skips when the confirm dialog returns false', async () => {
      confirm.ask.mockResolvedValueOnce(false);
      await component.remove(makeRec());
      expect(recordings.remove).not.toHaveBeenCalled();
    });

    it('deletes, toasts, and refreshes on confirm', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      await component.remove(makeRec());
      expect(recordings.remove).toHaveBeenCalledWith('r1');
      expect(toast.success).toHaveBeenCalled();
    });

    it('shows an error toast when the service throws', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      recordings.remove.mockReturnValueOnce(throwError(() => ({ message: 'denied' })));
      await component.remove(makeRec());
      expect(toast.error).toHaveBeenCalledWith('denied');
    });

    it('falls back to default error message', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      recordings.remove.mockReturnValueOnce(throwError(() => ({})));
      await component.remove(makeRec());
      expect(toast.error).toHaveBeenCalledWith('Failed to delete recording.');
    });
  });

  describe('formatters', () => {
    it('formatBytes returns em-dash for null', () => {
      expect(component.formatBytes(null)).toBe('—');
      expect(component.formatBytes(undefined)).toBe('—');
    });

    it.each([
      [512, '512 B'],
      [2048, '2.0 KB'],
      [5 * 1024 * 1024, '5.0 MB'],
      [3 * 1024 * 1024 * 1024, '3.0 GB'],
    ])('formatBytes(%s) === %s', (bytes, expected) => {
      expect(component.formatBytes(bytes)).toBe(expected);
    });

    it('formatDuration handles seconds / minutes / hours', () => {
      expect(component.formatDuration(null)).toBe('—');
      expect(component.formatDuration(45)).toBe('45s');
      expect(component.formatDuration(125)).toBe('2m 5s');
      expect(component.formatDuration(3725)).toBe('1h 2m');
    });

    it.each([
      ['Available', 'text-tertiary'],
      ['Recording', 'text-primary'],
      ['Uploading', 'text-primary'],
      ['Failed', 'text-error'],
      ['Deleted', 'text-on-surface-variant'],
    ] as const)('statusClass(%s) === %s', (status, expected) => {
      expect(component.statusClass(status)).toBe(expected);
    });
  });

  describe('isOwnerOrAdmin', () => {
    it('returns true when current user has Owner role', () => {
      auth.currentUser.set({ roles: ['Owner'] });
      expect(component.isOwnerOrAdmin()).toBe(true);
    });

    it('returns true when current user has Admin role', () => {
      auth.currentUser.set({ roles: ['Admin', 'User'] });
      expect(component.isOwnerOrAdmin()).toBe(true);
    });

    it('returns false for ordinary users', () => {
      auth.currentUser.set({ roles: ['User'] });
      expect(component.isOwnerOrAdmin()).toBe(false);
    });

    it('returns false when no user is loaded', () => {
      auth.currentUser.set(null);
      expect(component.isOwnerOrAdmin()).toBe(false);
    });
  });
});
