import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';

import { AuditLogs } from './audit-logs';
import { AuditLogsService, AuditLogEntry } from '../../../services/audit-logs.service';

describe('AuditLogs', () => {
  let component: AuditLogs;
  let fixture: ComponentFixture<AuditLogs>;
  let svc: {
    result: ReturnType<typeof signal<{ items: AuditLogEntry[]; totalPages: number }>>;
    reload: ReturnType<typeof vi.fn>;
    exportCsvUrl: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    svc = {
      result: signal({ items: [] as AuditLogEntry[], totalPages: 5 }),
      reload: vi.fn(() => of(undefined)),
      exportCsvUrl: vi.fn(() => '/api/audit-logs/export.csv'),
    };

    await TestBed.configureTestingModule({
      imports: [AuditLogs],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: AuditLogsService, useValue: svc },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      pages: {
        auditing: {
          table: {
            unknownUser: 'unknown',
            rowAriaLabel: 'View details for {{action}} by {{actor}}',
          },
          detail: {
            noDetails: '(no details)',
          },
        },
      },
    });
    translate.use('en');

    fixture = TestBed.createComponent(AuditLogs);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('search/load lifecycle', () => {
    it('ngOnInit triggers search', () => {
      const spy = vi.spyOn(component, 'search');
      component.ngOnInit();
      expect(spy).toHaveBeenCalled();
    });

    it('search() resets to page 1 and calls reload', () => {
      component.page.set(10);
      component.search();
      expect(component.page()).toBe(1);
      expect(svc.reload).toHaveBeenCalled();
      expect(component.loading()).toBe(false);
    });

    it('reload error stops loading', () => {
      svc.reload.mockReturnValueOnce(throwError(() => new Error('x')));
      component.search();
      expect(component.loading()).toBe(false);
    });

    it('goToPage moves to valid page and reloads', () => {
      svc.result.set({ items: [], totalPages: 5 });
      component.goToPage(3);
      expect(component.page()).toBe(3);
      expect(svc.reload).toHaveBeenCalled();
    });

    it('goToPage rejects page < 1', () => {
      const before = svc.reload.mock.calls.length;
      component.goToPage(0);
      expect(svc.reload.mock.calls.length).toBe(before);
    });

    it('goToPage rejects page > totalPages when totalPages > 0', () => {
      svc.result.set({ items: [], totalPages: 5 });
      const before = svc.reload.mock.calls.length;
      component.goToPage(99);
      expect(svc.reload.mock.calls.length).toBe(before);
    });

    it('goToPage allows any page when totalPages is 0', () => {
      svc.result.set({ items: [], totalPages: 0 });
      component.goToPage(42);
      expect(component.page()).toBe(42);
    });
  });

  describe('reset()', () => {
    it('clears filters and re-searches', () => {
      component.user.set('alice');
      component.action.set('user.create');
      component.resourceType.set('User');
      component.from.set('2025-01-01');
      component.to.set('2025-01-31');
      component.outcome.set('failure');
      component.page.set(5);
      component.reset();
      expect(component.user()).toBe('');
      expect(component.action()).toBe('');
      expect(component.resourceType()).toBe('');
      expect(component.from()).toBe('');
      expect(component.to()).toBe('');
      expect(component.outcome()).toBe('');
      expect(component.page()).toBe(1);
    });
  });

  describe('actionCategory()', () => {
    it.each([
      ['', 'action-default'],
      ['user.created', 'action-user'],
      ['invite.sent', 'action-invite'],
      ['connection.ticket.issued', 'action-ticket'],
      ['connection.created', 'action-connection'],
      ['system.startup', 'action-system'],
      ['something.failed', 'action-error'],
      ['login.denied', 'action-error'],
      ['internal.error', 'action-error'],
      ['unknown.event', 'action-default'],
    ])('actionCategory(%s) -> %s', (input, expected) => {
      expect(component.actionCategory(input)).toBe(expected);
    });
  });

  describe('outcomeBadgeClass()', () => {
    it('returns failure or success class', () => {
      expect(component.outcomeBadgeClass('failure')).toBe('outcome-badge-failure');
      expect(component.outcomeBadgeClass('success')).toBe('outcome-badge-success');
      expect(component.outcomeBadgeClass('')).toBe('outcome-badge-success');
    });
  });

  describe('formattedDetails()', () => {
    it('pretty-prints valid JSON', () => {
      const r = component.formattedDetails('{"a":1}');
      expect(r).toContain('"a": 1');
    });

    it('returns "(no details)" for empty JSON object', () => {
      expect(component.formattedDetails('{}')).toBe('(no details)');
    });

    it('returns raw text for invalid JSON', () => {
      expect(component.formattedDetails('not json')).toBe('not json');
    });

    it('returns "(no details)" for empty invalid input', () => {
      expect(component.formattedDetails('')).toBe('(no details)');
    });
  });

  describe('selectEntry / closeDetail', () => {
    it('selectEntry sets entry', () => {
      const e = { id: '1' } as unknown as AuditLogEntry;
      component.selectEntry(e);
      expect(component.selectedEntry()).toBe(e);
    });

    it('closeDetail clears entry', () => {
      const e = { id: '1' } as unknown as AuditLogEntry;
      component.selectEntry(e);
      component.closeDetail();
      expect(component.selectedEntry()).toBeNull();
    });
  });

  describe('page size', () => {
    it('setPageSize updates and reloads', () => {
      const before = svc.reload.mock.calls.length;
      component.setPageSize(50);
      expect(component.pageSize()).toBe(50);
      expect(component.page()).toBe(1);
      expect(svc.reload.mock.calls.length).toBe(before + 1);
    });

    it('onPageSizeChange accepts numeric string', () => {
      component.onPageSizeChange('100');
      expect(component.pageSize()).toBe(100);
    });

    it('onPageSizeChange ignores invalid input', () => {
      component.pageSize.set(25);
      component.onPageSizeChange('not-a-number');
      expect(component.pageSize()).toBe(25);
    });

    it('onPageSizeChange ignores < 1', () => {
      component.pageSize.set(25);
      component.onPageSizeChange(0);
      expect(component.pageSize()).toBe(25);
    });
  });

  describe('exportCsv()', () => {
    it('fetches blob and triggers download', async () => {
      const blob = new Blob(['x']);
      const fetchSpy = vi
        .spyOn(globalThis, 'fetch')
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        .mockResolvedValue({ blob: () => Promise.resolve(blob) } as any);
      const createObjectURLSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:url');
      const revokeSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
      const clickSpy = vi
        .spyOn(HTMLAnchorElement.prototype, 'click')
        .mockImplementation(() => undefined);

      component.user.set('alice');
      component.action.set('user.create');
      component.from.set('2025-01-01');
      component.to.set('2025-01-31');
      component.outcome.set('success');
      component.exportCsv();
      // Drain the fetch().then(blob).then(download) chain.
      for (let i = 0; i < 10; i++) await Promise.resolve();

      expect(svc.exportCsvUrl).toHaveBeenCalledWith(
        expect.objectContaining({
          user: 'alice',
          action: 'user.create',
          outcome: 'success',
          from: expect.any(String),
          to: expect.any(String),
        }),
      );
      expect(fetchSpy).toHaveBeenCalledWith('/api/audit-logs/export.csv', {
        credentials: 'include',
      });
      expect(createObjectURLSpy).toHaveBeenCalledWith(blob);
      expect(clickSpy).toHaveBeenCalled();
      expect(revokeSpy).toHaveBeenCalledWith('blob:url');
    });
  });

  describe('buildQuery (via load through search/setPageSize)', () => {
    it('appends user, action, resourceType, outcome', () => {
      component.user.set('  alice ');
      component.action.set('  user.create ');
      component.resourceType.set('  User ');
      component.outcome.set('failure');
      component.search();
      expect(svc.reload).toHaveBeenCalledWith(
        expect.objectContaining({
          user: 'alice',
          action: 'user.create',
          resourceType: 'User',
          outcome: 'failure',
          page: 1,
          pageSize: 25,
        }),
      );
    });

    it('appends from/to as ISO strings', () => {
      component.from.set('2025-01-01');
      component.to.set('2025-01-31');
      component.search();
      const q = svc.reload.mock.calls.at(-1)?.[0] as { from?: string; to?: string };
      expect(q.from).toContain('2025-01-01');
      expect(q.to).toContain('2025-01-31');
    });
  });
});
